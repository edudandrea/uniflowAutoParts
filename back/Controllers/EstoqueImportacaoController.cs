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
    [HttpPost("importar-nfe")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportarNfeResponse>> ImportarNfe([FromForm] IFormFile arquivo, [FromForm] long tenantId = 1)
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

        var jaImportada = await db.EntradasCompra.AnyAsync(entrada =>
            entrada.TenantId == tenantId &&
            entrada.ChaveNfe == chaveNfe);

        if (jaImportada)
        {
            return Conflict($"NF-e {chaveNfe} ja foi importada.");
        }

        var ide = Filho(infNfe, "ide");
        var emit = Filho(infNfe, "emit");
        var dest = Filho(infNfe, "dest");
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

        await using var transaction = await db.Database.BeginTransactionAsync();

        var fornecedor = await ObterOuCriarFornecedor(tenantId, emit);
        var itens = await MontarItens(infNfe, tenantId);

        if (itens.Count == 0)
        {
            return BadRequest("XML de NF-e sem itens para importar.");
        }

        var entrada = new EntradaCompra
        {
            TenantId = tenantId,
            FornecedorId = fornecedor.Id,
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
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

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
        await db.SaveChangesAsync();

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

    private static ImportarNfeResponse ToResponse(EntradaCompra entrada, Cliente fornecedor)
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
            fornecedor.NomeRazaoSocial,
            fornecedor.CpfCnpj,
            entrada.ValorProdutos,
            entrada.ValorFrete,
            entrada.ValorSeguro,
            entrada.ValorDesconto,
            entrada.ValorOutrasDespesas,
            entrada.ValorTotal,
            itens.Count,
            produtosIdentificados,
            itens.Count - produtosIdentificados,
            itens);
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
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
    }

    private static string ApenasDigitos(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }
}
