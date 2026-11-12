using System.Globalization;
using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Services;

// Apenas prepara as entidades. O chamador salva produtos, vinculos e nota juntos.
public sealed class NfeProdutosService(AppDbContext db)
{
    public async Task AssociarAsync(EntradaCompra entrada)
    {
        var pendentes = entrada.Itens.Where(i => i.ProdutoId is null && i.Produto is null).ToList();
        if (pendentes.Count == 0) return;

        var empresaId = checked((int)entrada.TenantId);
        var skus = (await db.CadastroItens.Where(p => p.EmpresaId == empresaId).Select(p => p.SKU).ToListAsync()).ToHashSet();
        var proximoSku = skus.Count == 0 ? 1 : skus.Max() == int.MaxValue ? 1 : Math.Max(1, skus.Max() + 1);
        var produtosDaNota = new Dictionary<string, CadastroItens>(StringComparer.Ordinal);
        Categoria? categoria = null;
        Marca? marca = null;

        foreach (var item in pendentes)
        {
            var ean = EanValido(item.Ean) ? item.Ean : null;
            var descricao = item.DescricaoXml.Trim();
            var ncm = Inteiro(item.Ncm);
            var identidade = !string.IsNullOrWhiteSpace(item.CodigoFornecedor) ? "codigo:" + item.CodigoFornecedor
                : ean is not null ? "ean:" + ean : $"descricao:{descricao.ToUpperInvariant()}|{ncm}";

            if (!produtosDaNota.TryGetValue(identidade, out var produto) &&
                !(ean is not null && produtosDaNota.TryGetValue("ean:" + ean, out produto)))
            {
                // O codigo do fornecedor pertence ao emitente, nao ao nosso SKU.
                var historico = db.EntradaCompraItens.Where(i => i.EntradaCompra!.TenantId == entrada.TenantId &&
                    i.Produto != null && i.Produto.EmpresaId == empresaId);
                if (!string.IsNullOrWhiteSpace(item.CodigoFornecedor) && entrada.FornecedorId.HasValue)
                {
                    produto = await historico.Where(i => i.EntradaCompra!.FornecedorId == entrada.FornecedorId &&
                            i.CodigoFornecedor == item.CodigoFornecedor)
                        .OrderByDescending(i => i.EntradaCompra!.ImportadoEm).ThenByDescending(i => i.Id)
                        .Select(i => i.Produto).FirstOrDefaultAsync();
                }
                if (produto is null && ean is not null)
                {
                    produto = await historico.Where(i => i.Ean == ean)
                        .OrderByDescending(i => i.EntradaCompra!.ImportadoEm).ThenByDescending(i => i.Id)
                        .Select(i => i.Produto).FirstOrDefaultAsync();
                }
                if (produto is null && descricao.Length > 0)
                {
                    produto = produtosDaNota.Values.FirstOrDefault(p => p.Descricao.Equals(descricao, StringComparison.OrdinalIgnoreCase) &&
                        (ncm == 0 || p.NCM == 0 || p.NCM == ncm));
                }
                if (produto is null && descricao.Length > 0)
                {
                    var descricaoLower = descricao.ToLowerInvariant();
                    var candidatos = await db.CadastroItens.Where(p => p.EmpresaId == empresaId &&
                        p.Descricao.Trim().ToLower() == descricaoLower && (ncm == 0 || p.NCM == 0 || p.NCM == ncm))
                        .Take(2).ToListAsync();
                    // Cadastros ambiguos ficam para associacao manual durante a conferencia.
                    if (candidatos.Count > 1) continue;
                    produto = candidatos.SingleOrDefault();
                }

                if (produto is null)
                {
                    categoria ??= await db.Categorias.FirstOrDefaultAsync(c => c.TenantId == entrada.TenantId &&
                        c.CategoriaPaiId == null && c.Nome == "Importados de NF-e");
                    if (categoria is null)
                    {
                        categoria = new Categoria { TenantId = entrada.TenantId, Nome = "Importados de NF-e",
                            Descricao = "Produtos cadastrados automaticamente pela importacao de NF-e.", Ativo = true, CriadoEm = DateTime.UtcNow };
                        db.Categorias.Add(categoria);
                    }
                    marca ??= await db.Marcas.FirstOrDefaultAsync(m => m.TenantId == entrada.TenantId && m.Nome == "Não informada");
                    if (marca is null)
                    {
                        marca = new Marca { TenantId = entrada.TenantId, Nome = "Não informada", Ativo = true, CriadoEm = DateTime.UtcNow };
                        db.Marcas.Add(marca);
                    }
                    var sku = proximoSku;
                    while (skus.Contains(sku)) sku = checked(sku + 1);
                    skus.Add(sku);
                    proximoSku = sku == int.MaxValue ? 1 : sku + 1;
                    produto = new CadastroItens
                    {
                        EmpresaId = empresaId, SKU = sku, Codintern = sku,
                        Descricao = descricao.Length > 0 ? descricao : $"Produto NF-e {item.CodigoFornecedor}",
                        Categoria = categoria, Marca = marca, NCM = ncm, CEST = Inteiro(item.Cest),
                        Ean = ean, Custo = (double)item.CustoUnitario, UnidadeComercial = item.UnidadeComercial.Length > 0 ? item.UnidadeComercial : "UN",
                        OrigemMercadoria = Origem(item.TributacaoXml), Ativo = true
                    };
                    db.CadastroItens.Add(produto);
                }
            }
            produtosDaNota[identidade] = produto;
            if (ean is not null) produtosDaNota.TryAdd("ean:" + ean, produto);
            item.Produto = produto;
        }
    }

    private static string Origem(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return "";
        try { return System.Xml.Linq.XElement.Parse(xml).Descendants().FirstOrDefault(e => e.Name.LocalName == "orig")?.Value ?? ""; }
        catch (System.Xml.XmlException) { return ""; }
    }
    private static int Inteiro(string? valor) => int.TryParse(valor, NumberStyles.None,
        CultureInfo.InvariantCulture, out var numero) ? numero : 0;

    private static bool EanValido(string? valor) => valor is not null && valor.Length is 8 or 12 or 13 or 14 &&
        valor.All(char.IsAsciiDigit) && valor.Any(c => c != '0');
}
