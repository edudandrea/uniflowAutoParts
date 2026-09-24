using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace back.Controllers;

[ApiController]
[Route("api/produtos")]
public class ProdutosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProdutoListaResponse>> Listar(
        [FromQuery] int empresaId,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50,
        [FromQuery] string? busca = null)
    {
        if (empresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        pagina = Math.Max(1, pagina);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 10, 100);

        var query = db.CadastroItens
            .AsNoTracking()
            .Where(produto => produto.EmpresaId == empresaId);

        var buscaNormalizada = TextoOuNull(busca);
        if (buscaNormalizada is not null)
        {
            var buscaLower = buscaNormalizada.ToLower();
            query = query.Where(produto =>
                produto.Descricao.ToLower().Contains(buscaLower) ||
                produto.SKU.ToString().Contains(buscaNormalizada) ||
                produto.Codfabricante.ToString().Contains(buscaNormalizada));
        }

        var total = await query.CountAsync();
        var totalAtivos = await query.CountAsync(produto => produto.Ativo);
        var produtos = await query
            .OrderBy(produto => produto.Descricao)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(produto => new ProdutoResponse(
                produto.Id,
                produto.EmpresaId,
                produto.SKU,
                produto.Codintern,
                produto.Codfabricante,
                produto.Descricao,
                produto.CategoriaId,
                produto.Categoria != null ? produto.Categoria.Nome : string.Empty,
                produto.MarcaId,
                produto.Marca != null ? produto.Marca.Nome : string.Empty,
                produto.FabricanteId,
                produto.Custo,
                produto.Preco,
                produto.EstoqueMinimo,
                produto.EstoqueMaximo,
                produto.NCM,
                produto.CEST,
                produto.OrigemMercadoria,
                produto.AliquotaIpi,
                produto.AliquotaIcms,
                produto.AliquotaMva,
                produto.ImpostosFabricante,
                produto.Ativo))
            .ToListAsync();

        return Ok(new ProdutoListaResponse(
            pagina,
            tamanhoPagina,
            total,
            totalAtivos,
            total - totalAtivos,
            produtos));
    }

    [HttpPost]
    public async Task<ActionResult<ProdutoResponse>> Criar(CriarProdutoRequest request)
    {
        if (request.EmpresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        var descricao = TextoOuNull(request.Descricao);
        if (descricao is null)
        {
            return BadRequest("Descricao e obrigatoria.");
        }

        var categoria = await db.Categorias.FirstOrDefaultAsync(categoria =>
            categoria.Id == request.CategoriaId &&
            categoria.TenantId == request.EmpresaId);

        if (categoria is null)
        {
            return BadRequest("Categoria nao encontrada para esta empresa.");
        }

        var marca = await db.Marcas.FirstOrDefaultAsync(marca =>
            marca.Id == request.MarcaId &&
            marca.TenantId == request.EmpresaId);

        if (marca is null)
        {
            return BadRequest("Marca nao encontrada para esta empresa.");
        }

        var skuJaExiste = await db.CadastroItens.AnyAsync(produto =>
            produto.EmpresaId == request.EmpresaId &&
            produto.SKU == request.SKU);

        if (skuJaExiste)
        {
            return Conflict("Ja existe um produto com este SKU para esta empresa.");
        }

        var produtoNovo = new CadastroItens
        {
            EmpresaId = request.EmpresaId,
            SKU = request.SKU,
            Codintern = request.Codintern,
            Codfabricante = request.Codfabricante,
            Descricao = descricao,
            CategoriaId = request.CategoriaId,
            Categoria = categoria,
            MarcaId = request.MarcaId,
            Marca = marca,
            FabricanteId = request.FabricanteId,
            Custo = request.Custo,
            Preco = request.Preco,
            EstoqueMinimo = request.EstoqueMinimo,
            EstoqueMaximo = request.EstoqueMaximo,
            NCM = request.Ncm,
            CEST = request.Cest,
            OrigemMercadoria = TextoOuNull(request.OrigemMercadoria) ?? string.Empty,
            AliquotaIpi = request.AliquotaIpi,
            AliquotaIcms = request.AliquotaIcms,
            AliquotaMva = request.AliquotaMva,
            ImpostosFabricante = TextoOuNull(request.ImpostosFabricante) ?? string.Empty,
            Ativo = request.Ativo
        };

        db.CadastroItens.Add(produtoNovo);
        await db.SaveChangesAsync();

        return Created($"/api/produtos/{produtoNovo.Id}", ToResponse(produtoNovo));
    }

    [HttpPost("importar-fabricante")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<ImportarProdutosFabricanteResponse>> ImportarFabricante(
        [FromForm] int empresaId,
        [FromForm] string fabricante,
        [FromForm] bool atualizarPrecos,
        [FromForm] IFormFile arquivo)
    {
        if (empresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        if (arquivo.Length == 0)
        {
            return BadRequest("Arquivo vazio.");
        }

        var fabricanteNormalizado = TextoOuNull(fabricante)?.ToUpperInvariant();
        if (fabricanteNormalizado is not "STELLANTIS")
        {
            return BadRequest("Fabricante nao suportado para importacao.");
        }

        var erros = new List<string>();
        var criados = 0;
        var atualizados = 0;
        var lidas = 0;
        var ignoradas = 0;
        var itensPorSku = new Dictionary<int, StellantisItem>();

        using var stream = arquivo.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);

        while (await reader.ReadLineAsync() is { } line)
        {
            lidas++;

            if (string.IsNullOrWhiteSpace(line))
            {
                ignoradas++;
                continue;
            }

            if (!TryParseStellantis(line, out var item, out var erro))
            {
                ignoradas++;
                if (erros.Count < 50)
                {
                    erros.Add($"Linha {lidas}: {erro}");
                }
                continue;
            }

            itensPorSku[item.CodigoProduto] = item;
        }

        var marca = await ObterOuCriarMarca(empresaId, "Stellantis", "STELLANTIS");
        var categoriasPorCodigo = await db.Categorias
            .Where(categoria => categoria.TenantId == empresaId && categoria.Nome.StartsWith("Stellantis "))
            .ToDictionaryAsync(categoria => categoria.Nome, StringComparer.OrdinalIgnoreCase);

        foreach (var categoriaCodigo in itensPorSku.Values.Select(item => item.CategoriaCodigo).Distinct())
        {
            await ObterOuCriarCategoria(empresaId, categoriaCodigo, categoriasPorCodigo);
        }

        var produtosPorSku = await db.CadastroItens
            .Where(produto => produto.EmpresaId == empresaId)
            .ToDictionaryAsync(produto => produto.SKU);

        foreach (var item in itensPorSku.Values)
        {
            var categoria = categoriasPorCodigo[$"Stellantis {item.CategoriaCodigo}"];
            var produto = produtosPorSku.GetValueOrDefault(item.CodigoProduto);

            if (produto is null)
            {
                produto = new CadastroItens
                {
                    EmpresaId = empresaId,
                    SKU = item.CodigoProduto,
                    Codintern = item.CodigoProduto,
                    Codfabricante = item.CodigoProduto,
                    Descricao = item.Descricao,
                    CategoriaId = categoria.Id,
                    Categoria = categoria,
                    MarcaId = marca.Id,
                    Marca = marca,
                    FabricanteId = (int)marca.Id,
                    Custo = item.Preco,
                    Preco = item.Preco,
                    EstoqueMinimo = 0,
                    EstoqueMaximo = 0,
                    NCM = item.Ncm,
                    CEST = 0,
                    OrigemMercadoria = item.Origem,
                    AliquotaIpi = item.AliquotaIpi,
                    AliquotaIcms = item.AliquotaIcms,
                    AliquotaMva = item.AliquotaMva,
                    ImpostosFabricante = item.ImpostosRaw,
                    Ativo = true
                };

                db.CadastroItens.Add(produto);
                produtosPorSku[item.CodigoProduto] = produto;
                criados++;
            }
            else
            {
                produto.Descricao = item.Descricao;
                produto.CategoriaId = categoria.Id;
                produto.Categoria = categoria;
                produto.MarcaId = marca.Id;
                produto.Marca = marca;
                produto.FabricanteId = (int)marca.Id;
                produto.NCM = item.Ncm;
                produto.OrigemMercadoria = item.Origem;
                produto.AliquotaIpi = item.AliquotaIpi;
                produto.AliquotaIcms = item.AliquotaIcms;
                produto.AliquotaMva = item.AliquotaMva;
                produto.ImpostosFabricante = item.ImpostosRaw;
                produto.Ativo = true;

                if (atualizarPrecos)
                {
                    produto.Custo = item.Preco;
                    produto.Preco = item.Preco;
                }

                atualizados++;
            }
        }

        await db.SaveChangesAsync();

        return Ok(new ImportarProdutosFabricanteResponse(
            "Stellantis",
            arquivo.FileName,
            lidas,
            criados,
            atualizados,
            ignoradas,
            erros,
            Array.Empty<ProdutoResponse>()));
    }

    private async Task<Marca> ObterOuCriarMarca(int empresaId, string nome, string codigo)
    {
        var marca = await db.Marcas.FirstOrDefaultAsync(marca =>
            marca.TenantId == empresaId &&
            (marca.Codigo == codigo || marca.Nome.ToLower() == nome.ToLower()));

        if (marca is not null)
        {
            return marca;
        }

        marca = new Marca
        {
            TenantId = empresaId,
            Nome = nome,
            Codigo = codigo,
            Descricao = "Criada automaticamente na importacao de produtos.",
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        db.Marcas.Add(marca);
        await db.SaveChangesAsync();
        return marca;
    }

    private async Task<Categoria> ObterOuCriarCategoria(
        int empresaId,
        string categoriaCodigo,
        Dictionary<string, Categoria> categoriasPorNome)
    {
        var nome = $"Stellantis {categoriaCodigo}";
        if (categoriasPorNome.TryGetValue(nome, out var categoria))
        {
            return categoria;
        }

        categoria = new Categoria
        {
            TenantId = empresaId,
            Nome = nome,
            Descricao = $"Categoria do arquivo Stellantis: {categoriaCodigo}",
            Icone = "category",
            Ordem = 0,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        categoriasPorNome[nome] = categoria;
        return categoria;
    }

    private static bool TryParseStellantis(string line, out StellantisItem item, out string erro)
    {
        item = default!;
        erro = string.Empty;

        if (line.Length < 163)
        {
            erro = "registro menor que o layout esperado.";
            return false;
        }

        var codigo = Inteiro(line, 0, 12);
        var descricao = Campo(line, 12, 15);
        var categoriaCodigo = Campo(line, 35, 6);
        var ncm = Inteiro(line, 41, 8);
        var origem = Campo(line, 49, 1);
        var preco = DecimalCentavos(line, 163, 16);
        var impostosRaw = Campo(line, 135, 26);
        var aliquotaIpi = Percentual(line, 135, 6, 2);
        var aliquotaIcms = Percentual(line, 141, 6, 3);
        var aliquotaMva = Percentual(line, 147, 6, 3);

        if (codigo <= 0)
        {
            erro = "codigo do produto invalido.";
            return false;
        }

        if (descricao.Length == 0)
        {
            erro = "descricao vazia.";
            return false;
        }

        item = new StellantisItem(codigo, descricao, categoriaCodigo, ncm, origem, preco, aliquotaIpi, aliquotaIcms, aliquotaMva, impostosRaw);
        return true;
    }

    private static ProdutoResponse ToResponse(CadastroItens produto)
    {
        return new ProdutoResponse(
            produto.Id,
            produto.EmpresaId,
            produto.SKU,
            produto.Codintern,
            produto.Codfabricante,
            produto.Descricao,
            produto.CategoriaId,
            produto.Categoria?.Nome ?? string.Empty,
            produto.MarcaId,
            produto.Marca?.Nome ?? string.Empty,
            produto.FabricanteId,
            produto.Custo,
            produto.Preco,
            produto.EstoqueMinimo,
            produto.EstoqueMaximo,
            produto.NCM,
            produto.CEST,
            produto.OrigemMercadoria,
            produto.AliquotaIpi,
            produto.AliquotaIcms,
            produto.AliquotaMva,
            produto.ImpostosFabricante,
            produto.Ativo);
    }

    private static string? TextoOuNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Campo(string value, int start, int length)
    {
        if (value.Length <= start)
        {
            return string.Empty;
        }

        return value.Substring(start, Math.Min(length, value.Length - start)).Trim();
    }

    private static int Inteiro(string value, int start, int length)
    {
        var digits = new string(Campo(value, start, length).Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static double DecimalCentavos(string value, int start, int length)
    {
        return Inteiro(value, start, length) / 100.0;
    }

    private static decimal Percentual(string value, int start, int length, int casas)
    {
        var divisor = (decimal)Math.Pow(10, casas);
        return divisor == 0 ? 0 : Inteiro(value, start, length) / divisor;
    }

    private record StellantisItem(
        int CodigoProduto,
        string Descricao,
        string CategoriaCodigo,
        int Ncm,
        string Origem,
        double Preco,
        decimal AliquotaIpi,
        decimal AliquotaIcms,
        decimal AliquotaMva,
        string ImpostosRaw);
}
