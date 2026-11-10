using System.Globalization;
using System.Xml.Linq;
using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/estoque")]
public class EstoqueImportacaoController(AppDbContext db) : ControllerBase
{
    [HttpGet("entradas")]
    public async Task<ActionResult<IReadOnlyCollection<EntradaCompraResumoResponse>>> ListarEntradas([FromQuery] long tenantId = 0)
    {
        if (tenantId <= 0) return BadRequest("TenantId e obrigatorio.");
        return Ok(await db.EntradasCompra.AsNoTracking().Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.ImportadoEm).ThenByDescending(e => e.Id)
            .Select(e => new EntradaCompraResumoResponse(e.Id, e.NumeroNfe, e.Serie, e.ChaveNfe,
                e.Fornecedor != null ? e.Fornecedor.NomeRazaoSocial : "Fornecedor nao encontrado",
                e.ImportadoEm, e.ValorTotal, e.Status)).ToListAsync());
    }

    [HttpGet("entradas/{id:long}")]
    public async Task<ActionResult<ImportarNfeResponse>> ObterEntrada(long id, [FromQuery] long tenantId = 0)
    {
        if (tenantId <= 0) return BadRequest("TenantId e obrigatorio.");
        var entrada = await db.EntradasCompra.AsNoTracking().Include(e => e.Fornecedor).Include(e => e.Itens)
            .SingleOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
        return entrada is null ? NotFound() : Ok(ToResponse(entrada, entrada.Fornecedor));
    }

    [HttpPut("entradas/{id:long}/conferencia")]
    public async Task<ActionResult<ImportarNfeResponse>> SalvarConferencia(long id, [FromBody] ConferirNfeRequest request,
        [FromQuery] long tenantId = 0)
    {
        if (tenantId <= 0) return BadRequest("TenantId e obrigatorio.");
        var entrada = await db.EntradasCompra.Include(e => e.Fornecedor).Include(e => e.Itens)
            .SingleOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
        if (entrada is null) return NotFound();
        if (entrada.Status == "Conferida") return Conflict("Esta NF-e ja foi conferida.");
        if (request.Itens is null || request.Itens.Count != entrada.Itens.Count ||
            request.Itens.Select(i => i.NumeroItem).Distinct().Count() != entrada.Itens.Count ||
            request.Itens.Any(i => !entrada.Itens.Any(original => original.NumeroItem == i.NumeroItem)))
            return BadRequest("Informe todos os itens da nota, sem repeticoes.");
        if (request.Itens.Any(i => i.QuantidadeRecebida < 0 || i.QuantidadeRecebida >= 100000000000000m ||
            decimal.Round(i.QuantidadeRecebida, 4) != i.QuantidadeRecebida))
            return BadRequest("Informe quantidades recebidas validas, com ate quatro casas decimais.");
        var produtoIds = request.Itens.Where(i => i.ProdutoId.HasValue).Select(i => i.ProdutoId!.Value).Distinct().ToArray();
        var produtosValidos = await db.CadastroItens.AsNoTracking()
            .CountAsync(p => p.EmpresaId == tenantId && produtoIds.Contains(p.Id));
        if (produtosValidos != produtoIds.Length) return BadRequest("Selecione produtos cadastrados nesta empresa.");
        if (request.Concluir && request.Itens.Any(i => i.ProdutoId is null))
            return BadRequest("Associe todos os itens a produtos antes de concluir a conferencia.");
        var recebidos = request.Itens.ToDictionary(i => i.NumeroItem);
        foreach (var item in entrada.Itens)
        {
            var recebido = recebidos[item.NumeroItem];
            item.ProdutoId = recebido.ProdutoId;
            item.QuantidadeRecebida = recebido.QuantidadeRecebida;
        }
        if (request.Concluir) entrada.Status = "Conferida";
        await db.SaveChangesAsync();
        return Ok(ToResponse(entrada, entrada.Fornecedor));
    }

    [HttpPost("importar-nfe")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportarNfeResponse>> ImportarNfe([FromForm] IFormFile arquivo, [FromForm] long tenantId = 0)
    {
        if (tenantId <= 0)
        {
            return BadRequest("TenantId e obrigatorio.");
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest("Informe o XML da NF-e.");
        }

        XDocument document;
        try
        {
            await using var stream = arquivo.OpenReadStream();
            document = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, HttpContext.RequestAborted);
        }
        catch
        {
            return BadRequest("Arquivo XML invalido ou corrompido.");
        }

        var nfe = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "NFe");
        var infNfe = nfe?.Descendants().FirstOrDefault(element => element.Name.LocalName == "infNFe");

        if (infNfe is null)
        {
            return BadRequest("Este XML nao e uma NF-e de compra de itens. Use uma NF-e modelo 55 autorizada.");
        }

        var chaveNfe = ObterChaveNfe(document, infNfe);
        if (string.IsNullOrWhiteSpace(chaveNfe) || chaveNfe.Length != 44 || !chaveNfe.All(char.IsDigit))
        {
            return BadRequest("Chave de acesso da NF-e invalida.");
        }

        var jaImportada = await db.EntradasCompra.AsNoTracking().Where(entrada =>
            entrada.TenantId == tenantId &&
            entrada.ChaveNfe == chaveNfe).Select(entrada => (long?)entrada.Id).FirstOrDefaultAsync();

        if (jaImportada.HasValue)
        {
            return Conflict(new { message = $"NF-e {chaveNfe} ja foi importada.", entradaCompraId = jaImportada.Value });
        }

        var ide = Filho(infNfe, "ide");
        var emit = Filho(infNfe, "emit");
        var total = infNfe.Descendants().FirstOrDefault(element => element.Name.LocalName == "ICMSTot");

        if (ide is null || emit is null)
        {
            return BadRequest("XML de NF-e invalido: blocos ide ou emit nao encontrados.");
        }

        var erroTipoNota = ValidarNotaCompraItens(infNfe, ide);
        if (erroTipoNota is not null)
        {
            return BadRequest(erroTipoNota);
        }

        var fornecedor = await ObterOuCriarFornecedor(tenantId, emit);
        var itens = await MontarItens(infNfe, tenantId);

        if (itens.Count == 0)
        {
            return BadRequest("XML de NF-e sem itens para importar.");
        }

        var entrada = new EntradaCompra
        {
            TenantId = tenantId,
            Fornecedor = fornecedor,
            ChaveNfe = chaveNfe,
            NumeroNfe = Texto(ide, "nNF") ?? string.Empty,
            Serie = Texto(ide, "serie") ?? string.Empty,
            Modelo = Texto(ide, "mod") ?? string.Empty,
            DataEmissao = Data(Texto(ide, "dhEmi")),
            DataEntrada = Data(Texto(ide, "dhSaiEnt")),
            TipoOperacao = Texto(ide, "tpNF") ?? string.Empty,
            Finalidade = Texto(ide, "finNFe") ?? string.Empty,
            NaturezaOperacao = Texto(ide, "natOp") ?? string.Empty,
            ValorProdutos = Decimal(total, "vProd"),
            ValorFrete = Decimal(total, "vFrete"),
            ValorSeguro = Decimal(total, "vSeg"),
            ValorDesconto = Decimal(total, "vDesc"),
            ValorOutrasDespesas = Decimal(total, "vOutro"),
            ValorTotal = Decimal(total, "vNF"),
            XmlOriginal = document.ToString(SaveOptions.DisableFormatting),
            Itens = itens
        };

        db.EntradasCompra.Add(entrada);
        // Uma unica gravacao usa a transacao implicita do EF, compativel com
        // EnableRetryOnFailure, e salva fornecedor, nota e itens atomicamente.
        await db.SaveChangesAsync();

        return Created($"/api/estoque/entradas/{entrada.Id}", ToResponse(entrada, fornecedor));
    }

    private async Task<Cliente> ObterOuCriarFornecedor(long tenantId, XElement emit)
    {
        var documento = ApenasDigitos(Texto(emit, "CNPJ") ?? Texto(emit, "CPF") ?? string.Empty);
        var fornecedor = await db.Clientes.FirstOrDefaultAsync(cliente =>
            cliente.TenantId == tenantId &&
            cliente.CpfCnpj == documento);

        if (fornecedor is not null)
        {
            if (fornecedor.Relacionamento == RelacionamentoCadastro.Cliente)
            {
                fornecedor.Relacionamento = RelacionamentoCadastro.Ambos;
            }

            return fornecedor;
        }

        var nome = Texto(emit, "xNome") ?? "Fornecedor NF-e";
        fornecedor = new Cliente
        {
            TenantId = tenantId,
            TipoPessoa = documento.Length == 11 ? TipoPessoa.fisica : TipoPessoa.juridica,
            Relacionamento = RelacionamentoCadastro.Fornecedor,
            NomeRazaoSocial = nome,
            NomeFantasia = Texto(emit, "xFant"),
            CpfCnpj = documento,
            InscricaoEstadual = Texto(emit, "IE"),
            IndicadorIe = string.IsNullOrWhiteSpace(Texto(emit, "IE")) ? IndicadorIe.NaoContribuinte : IndicadorIe.Contribuinte,
            ConsumidorFinal = false,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        db.Clientes.Add(fornecedor);

        return fornecedor;
    }

    private async Task<List<EntradaCompraItem>> MontarItens(XElement infNfe, long tenantId)
    {
        var itens = new List<EntradaCompraItem>();

        foreach (var det in infNfe.Elements().Where(element => element.Name.LocalName == "det"))
        {
            var prod = Filho(det, "prod");
            if (prod is null)
            {
                continue;
            }

            var codigoFornecedor = Texto(prod, "cProd") ?? string.Empty;
            var produto = int.TryParse(codigoFornecedor, out var sku)
                ? await db.CadastroItens.AsNoTracking().FirstOrDefaultAsync(item => item.EmpresaId == tenantId && item.SKU == sku)
                : null;

            var quantidadeComercial = Decimal(prod, "qCom");
            var valorProduto = Decimal(prod, "vProd");
            var tributacaoXml = det.Elements().FirstOrDefault(element => element.Name.LocalName == "imposto")?.ToString(SaveOptions.DisableFormatting);

            itens.Add(new EntradaCompraItem
            {
                ProdutoId = produto?.Id,
                NumeroItem = int.TryParse(det.Attribute("nItem")?.Value, out var numeroItem) ? numeroItem : itens.Count + 1,
                CodigoFornecedor = codigoFornecedor,
                Ean = Texto(prod, "cEAN"),
                DescricaoXml = Texto(prod, "xProd") ?? string.Empty,
                Ncm = Texto(prod, "NCM"),
                Cest = Texto(prod, "CEST"),
                Cfop = Texto(prod, "CFOP"),
                UnidadeComercial = Texto(prod, "uCom") ?? string.Empty,
                QuantidadeComercial = quantidadeComercial,
                ValorUnitarioComercial = Decimal(prod, "vUnCom"),
                UnidadeTributavel = Texto(prod, "uTrib") ?? string.Empty,
                QuantidadeTributavel = Decimal(prod, "qTrib"),
                ValorUnitarioTributavel = Decimal(prod, "vUnTrib"),
                ValorProduto = valorProduto,
                ValorFrete = Decimal(prod, "vFrete"),
                ValorSeguro = Decimal(prod, "vSeg"),
                ValorDesconto = Decimal(prod, "vDesc"),
                ValorOutrasDespesas = Decimal(prod, "vOutro"),
                QuantidadeRecebida = quantidadeComercial,
                CustoUnitario = quantidadeComercial > 0 ? valorProduto / quantidadeComercial : 0,
                PedidoCompra = Texto(prod, "xPed"),
                ItemPedido = Texto(prod, "nItemPed"),
                TributacaoXml = tributacaoXml
            });
        }

        return itens;
    }

    private static string? ValidarNotaCompraItens(XElement infNfe, XElement ide)
    {
        if (Texto(ide, "mod") != "55")
        {
            return "Este XML nao e uma NF-e modelo 55 de compra de itens. Notas de servico ou outros documentos nao podem ser importados pelo estoque.";
        }

        var possuiItensProduto = infNfe
            .Elements()
            .Where(element => element.Name.LocalName == "det")
            .Any(det => Filho(det, "prod") is not null);

        if (!possuiItensProduto)
        {
            return "A nota fiscal nao possui itens de produto para entrada em estoque.";
        }

        return null;
    }

    private static ImportarNfeResponse ToResponse(EntradaCompra entrada, Cliente? fornecedor)
    {
        var itens = entrada.Itens
            .OrderBy(item => item.NumeroItem)
            .Select(item => new ImportarNfeItemResponse(
                item.NumeroItem,
                item.CodigoFornecedor,
                item.Ean,
                item.DescricaoXml,
                item.Ncm,
                item.Cest,
                item.Cfop,
                item.UnidadeComercial,
                item.QuantidadeComercial,
                item.ValorUnitarioComercial,
                item.UnidadeTributavel,
                item.QuantidadeTributavel,
                item.ValorUnitarioTributavel,
                item.ValorProduto,
                item.ValorFrete,
                item.ValorSeguro,
                item.ValorDesconto,
                item.ValorOutrasDespesas,
                item.QuantidadeRecebida,
                item.CustoUnitario,
                item.ProdutoId is not null,
                item.ProdutoId))
            .ToList();

        var produtosIdentificados = itens.Count(item => item.ProdutoIdentificado);

        return new ImportarNfeResponse(
            entrada.Id,
            entrada.ChaveNfe,
            entrada.NumeroNfe,
            entrada.Serie,
            entrada.Modelo,
            entrada.DataEmissao,
            entrada.DataEntrada,
            entrada.NaturezaOperacao,
            fornecedor?.NomeRazaoSocial ?? "Fornecedor nao encontrado",
            fornecedor?.CpfCnpj ?? string.Empty,
            entrada.ValorProdutos,
            entrada.ValorFrete,
            entrada.ValorSeguro,
            entrada.ValorDesconto,
            entrada.ValorOutrasDespesas,
            entrada.ValorTotal,
            itens.Count,
            produtosIdentificados,
            itens.Count - produtosIdentificados,
            itens,
            entrada.Status);
    }

    private static string ObterChaveNfe(XDocument document, XElement infNfe)
    {
        var id = infNfe.Attribute("Id")?.Value ?? string.Empty;
        if (id.StartsWith("NFe", StringComparison.OrdinalIgnoreCase))
        {
            return id[3..];
        }

        return document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "chNFe")
            ?.Value
            .Trim() ?? string.Empty;
    }

    private static XElement? Filho(XElement? element, string name)
    {
        return element?.Elements().FirstOrDefault(child => child.Name.LocalName == name);
    }

    private static string? Texto(XElement? element, string name)
    {
        var value = Filho(element, name)?.Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static decimal Decimal(XElement? element, string name)
    {
        var value = Texto(element, name);
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static DateTime? Data(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed) ? parsed.UtcDateTime : null;
    }

    private static string ApenasDigitos(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }
}
