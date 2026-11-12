using System.Text;
using System.Xml.Linq;
using back.Contracts;
using back.Controllers;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal static class NfeProductChecks
{
    public static async Task RunAsync()
    {
        var options = await NfeTestDatabase.CreateAsync(1, 2);
        TenantContext Tenant(int id) => new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(id) }
        });
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }
        async Task<ActionResult<ImportarNfeResponse>> Import(string xml, int tenantId = 1)
        {
            await using var db = new AppDbContext(options, Tenant(tenantId));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            var controller = new EstoqueImportacaoController(db)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            return await controller.ImportarNfe(new FormFile(stream, 0, stream.Length, "arquivo", "nota.xml"), tenantId);
        }
        ImportarNfeResponse Created(ActionResult<ImportarNfeResponse> result) =>
            (ImportarNfeResponse)((CreatedResult)result.Result!).Value!;

        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            db.Categorias.Add(new Categoria { Id = 201, TenantId = 1, Nome = "Categoria existente" });
            db.Marcas.Add(new Marca { Id = 201, TenantId = 1, Nome = "Marca existente" });
            db.CadastroItens.Add(new CadastroItens { Id = 10, EmpresaId = 1, SKU = 10,
                Descricao = "Peca existente", NCM = 87089990, CategoriaId = 201, MarcaId = 201, Custo = 99, Preco = 149, Ativo = true });
            await db.SaveChangesAsync();
        }
        var filtro = Item(1, "ABC-001", "Filtro de oleo", "84212300", "7891234567895");
        var xml = Nota(1, "12345678000195", filtro,
            Item(2, "EXIST-X", "Peca existente", "87089990"),
            Item(3, "ABC-001", "Filtro de oleo", "84212300", "7891234567895"),
            Item(4, "OUTRO-CODIGO", "Filtro com outro codigo", "84212300", "7891234567895"));
        var importada = Created(await Import(xml));
        var filtroId = importada.Itens.First().ProdutoId;
        var mesmaNota = await Import(xml);
        Check(mesmaNota.Result is OkObjectResult { Value: ImportarNfeResponse { JaImportada: true } },
            "Mesma nota nao foi reaberta como sucesso.");
        Check(importada.ProdutosIdentificados == 4 && importada.ProdutosPendentes == 0, "Itens novos nao foram associados automaticamente.");
        Check(importada.Itens.ElementAt(1).ProdutoId == 10, "Nao reutilizou produto previamente cadastrado.");
        Check(filtroId > 0 && importada.Itens.Where(i => i.NumeroItem != 2).All(i => i.ProdutoId == filtroId),
            "Codigo repetido ou mesmo EAN na nota gerou produtos duplicados.");
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var produto = await db.CadastroItens.Include(p => p.Marca).Include(p => p.Categoria).SingleAsync(p => p.Id == filtroId);
            Check(await db.CadastroItens.CountAsync() == 2, "Cadastro automatico criou produtos extras.");
            Check(produto.Descricao == "Filtro de oleo" && produto.NCM == 84212300 && produto.CEST == 100200 && produto.Custo == 12.5,
                "Cadastro automatico perdeu descricao, NCM, CEST ou custo.");
            Check(produto.SKU > 10 && produto.Ativo && produto.Preco == 0 && produto.Codfabricante == 0,
                "SKU invalido ou codigo do fornecedor/preco de compra confundido com fabricante/preco de venda.");
            Check(produto.Categoria!.Nome == "Importados de NF-e" && produto.Marca!.Nome == "Não informada",
                "Categoria e marca de revisao nao foram criadas.");
            var existente = await db.CadastroItens.SingleAsync(p => p.Id == 10);
            Check(existente.Custo == 99 && existente.Preco == 149 && existente.MarcaId == 201,
                "Importacao sobrescreveu cadastro existente.");
            Check(await db.Estoques.CountAsync() == 4 && await db.Estoques.Where(m => m.ItemId == filtroId).SumAsync(m => m.Quantidade) == 6m,
                "Importacao nao lancou as quantidades de todos os itens, ou reimportacao duplicou o estoque.");
            var saldo = (IReadOnlyCollection<EstoqueResponse>)((OkObjectResult)(await new EstoqueController(db).Listar(1, CancellationToken.None)).Result!).Value!;
            Check(saldo.Single(p => p.Id == filtroId).Estoque == 6m, "Saldo nao foi refletido pela API do estoque.");
            Check(saldo.Single(p => p.Id == filtroId).Valor == 12.5,
                "Estoque nao exibiu o custo importado para produto sem preco de venda.");
            Check(saldo.Single(p => p.Id == 10).Valor == 149,
                "Estoque substituiu o preco de venda cadastrado pelo custo da nota.");
        }

        // Movimentos anteriores permanecem; reimportacao e conferencia alteram somente os da nota.
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            db.Estoques.Add(new Estoque { EmpresaId = 1, ItemId = filtroId!.Value, UsuarioId = 1,
                Tipo = "Entrada", Quantidade = 10, Documento = "Movimento anterior" });
            await db.SaveChangesAsync();
        }
        await Import(xml);
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            Check(await db.Estoques.Where(m => m.ItemId == filtroId).SumAsync(m => m.Quantidade) == 16m,
                "Reimportacao duplicou a quantidade ou alterou movimentos anteriores.");
            var itens = importada.Itens.Select(i => new ConferirNfeItemRequest(i.NumeroItem, i.ProdutoId,
                i.NumeroItem == 1 ? 0.75m : i.QuantidadeRecebida)).ToArray();
            var controller = new EstoqueImportacaoController(db);
            Check((await controller.SalvarConferencia(importada.EntradaCompraId, new(itens), 1)).Result is OkObjectResult,
                "Nao salvou ajuste de quantidade recebida.");
            Check(await db.Estoques.CountAsync() == 5 && await db.Estoques.Where(m => m.ItemId == filtroId).SumAsync(m => m.Quantidade) == 14.75m,
                "Ajuste de conferencia somou novo movimento em vez de corrigir o existente.");
            itens[0] = itens[0] with { ProdutoId = 10 };
            await controller.SalvarConferencia(importada.EntradaCompraId, new(itens), 1);
            Check(await db.Estoques.Where(m => m.ItemId == 10).SumAsync(m => m.Quantidade) == 2.75m &&
                await db.Estoques.Where(m => m.ItemId == filtroId).SumAsync(m => m.Quantidade) == 14m,
                "Troca de associacao nao transferiu a entrada para o produto correto.");
            itens[0] = itens[0] with { QuantidadeRecebida = 0 };
            await controller.SalvarConferencia(importada.EntradaCompraId, new(itens, true), 1);
            Check(await db.Estoques.CountAsync() == 4 && await db.Estoques.Where(m => m.ItemId == 10).SumAsync(m => m.Quantidade) == 2m,
                "Item nao recebido deixou saldo no estoque.");
        }
        await Import(xml);
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            Check(await db.Estoques.CountAsync() == 4 && await db.Estoques.Where(m => m.ItemId == filtroId).SumAsync(m => m.Quantidade) == 14m,
                "Reimportacao de nota conferida desfez a conferencia ou duplicou o saldo.");
        }

        var repetida = Created(await Import(Nota(2, "12345678000195", Item(1, "ABC-001", "Descricao alterada", "84212300"))));
        Check(repetida.Itens.Single().ProdutoId == filtroId, "Codigo alfanumerico do mesmo fornecedor nao foi reutilizado em outra nota.");
        var porEan = Created(await Import(Nota(3, "98765432000199", Item(1, "COD-NOVO", "Descricao diferente", "84212300", "7891234567895"))));
        Check(porEan.Itens.Single().ProdutoId == filtroId, "EAN de outra nota nao reutilizou produto conhecido.");
        var outroFornecedor = Created(await Import(Nota(4, "98765432000199", Item(1, "ABC-001", "Produto diferente", "87089990"))));
        Check(outroFornecedor.Itens.Single().ProdutoId != filtroId, "Misturou codigos iguais de fornecedores diferentes.");
        var colisaoSku = Created(await Import(Nota(5, "98765432000199", Item(1, "10", "Produto sem relacao com SKU 10", "87089990"))));
        Check(colisaoSku.Itens.Single().ProdutoId != 10, "Associou codigo numerico do fornecedor ao SKU errado.");
        var fracionado = Item(1, "FRAC", "Produto fracionado", "87089990");
        fracionado.Element("prod")!.Element("qCom")!.Value = "1.25";
        var notaFracionada = Created(await Import(Nota(6, "12345678000195", fracionado)));
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            Check((await db.Estoques.SingleAsync(m => m.ItemId == notaFracionada.Itens.Single().ProdutoId)).Quantidade == 1.25m,
                "Importacao arredondou quantidade fracionaria do XML.");
        }
        var outraEmpresa = Created(await Import(Nota(1, "12345678000195", filtro), 2));
        Check(outraEmpresa.Itens.Single().ProdutoId != filtroId, "Cadastro automatico associou produto de outra empresa.");
        await using (var db = new AppDbContext(options, Tenant(2)))
        {
            Check(await db.CadastroItens.CountAsync() == 1 && await db.Categorias.CountAsync() == 1 && await db.Marcas.CountAsync() == 1,
                "Cadastro automatico nao isolou produtos, categorias e marcas por empresa.");
        }

        long antigaId;
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var antiga = new EntradaCompra { TenantId = 1, ChaveNfe = new string('8', 44), NumeroNfe = "8",
                Itens = [new EntradaCompraItem { NumeroItem = 1, CodigoFornecedor = "OLD", DescricaoXml = "Produto de nota antiga",
                    Ncm = "87089990", QuantidadeComercial = 2, QuantidadeRecebida = 1.5m, CustoUnitario = 5 }] };
            db.EntradasCompra.Add(antiga);
            await db.SaveChangesAsync();
            antigaId = antiga.Id;
        }
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var controller = new EstoqueImportacaoController(db);
            var resposta = (ImportarNfeResponse)((OkObjectResult)(await controller.CadastrarProdutosPendentes(antigaId, 1)).Result!).Value!;
            Check(resposta.Itens.Single().ProdutoId > 0 && resposta.Itens.Single().QuantidadeRecebida == 1.5m,
                "Recuperacao de nota antiga nao cadastrou produto ou perdeu conferencia.");
            var antigoItemId = await db.EntradaCompraItens.Where(i => i.EntradaCompraId == antigaId).Select(i => i.Id).SingleAsync();
            Check((await db.Estoques.SingleAsync(m => m.EntradaCompraItemId == antigoItemId)).Quantidade == 1.5m,
                "Recuperacao da nota antiga perdeu a quantidade fracionaria.");
            var total = await db.CadastroItens.CountAsync();
            await controller.CadastrarProdutosPendentes(antigaId, 1);
            Check(await db.CadastroItens.CountAsync() == total, "Repetir cadastro pendente duplicou produto.");
            db.ChangeTracker.Clear();
            var entrada = await db.EntradasCompra.Include(e => e.Itens).SingleAsync(e => e.Id == antigaId);
            entrada.Itens.Single().ProdutoId = null;
            await db.SaveChangesAsync();
        }
        var duplicada = await Import(Nota(8, "12345678000195", Item(1, "OLD", "Produto de nota antiga", "87089990")));
        Check(duplicada.Result is OkObjectResult { Value: ImportarNfeResponse { JaImportada: true } }, "Reimportacao nao reabriu nota existente.");
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            Check((await db.EntradaCompraItens.SingleAsync(i => i.EntradaCompraId == antigaId)).ProdutoId > 0,
                "Reimportacao nao recuperou associacao de nota antiga.");
        }
        await using (var db = new AppDbContext(options, Tenant(2)))
        {
            Check((await new EstoqueImportacaoController(db).CadastrarProdutosPendentes(antigaId, 2)).Result is NotFoundResult,
                "Cadastro pendente acessou nota de outra empresa.");
        }
        Console.WriteLine($"NF-e automatic product checks: {checks} passed. Banco descartavel local.");
    }

    private static XElement Item(int numero, string codigo, string descricao, string ncm, string? ean = null) =>
        new("det", new XAttribute("nItem", numero), new XElement("prod", new XElement("cProd", codigo),
            new XElement("xProd", descricao), new XElement("NCM", ncm), new XElement("CEST", "0100200"),
            new XElement("cEAN", ean ?? "SEM GTIN"), new XElement("uCom", "UN"), new XElement("qCom", "2"),
            new XElement("vUnCom", "12.50"), new XElement("vProd", "25.00")));

    private static string Nota(int numero, string documento, params XElement[] itens) =>
        new XElement("nfeProc", new XElement("NFe", new XElement("infNFe", new XAttribute("Id", "NFe" + new string((char)('0' + numero), 44)),
            new XElement("ide", new XElement("mod", "55"), new XElement("nNF", numero)),
            new XElement("emit", new XElement("CNPJ", documento), new XElement("xNome", "Fornecedor de teste")), itens))).ToString();
}
