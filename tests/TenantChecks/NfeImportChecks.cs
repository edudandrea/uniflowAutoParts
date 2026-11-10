using System.Text;
using back.Contracts;
using back.Controllers;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class NfeImportChecks
{
    public static async Task RunAsync()
    {
        const string chave = "43261112345678000195550010000000011123456780";
        var xml = $"""
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe"><NFe><infNFe Id="NFe{chave}">
            <ide><mod>55</mod><nNF>1</nNF><serie>1</serie><dhEmi>2026-11-06T09:00:00-03:00</dhEmi></ide>
            <emit><CNPJ>12345678000195</CNPJ><xNome>Fornecedor de teste</xNome></emit>
            <det nItem="1"><prod><cProd>ABC</cProd><xProd>Peca de teste</xProd><uCom>UN</uCom><qCom>2</qCom><vUnCom>12.50</vUnCom><vProd>25.00</vProd></prod></det>
            <total><ICMSTot><vProd>25.00</vProd><vNF>25.00</vNF></ICMSTot></total>
            </infNFe></NFe></nfeProc>
            """;
        var tenant = new TenantContext(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(1) }
        });
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"nfe-import-{Guid.NewGuid()}").Options;

        async Task<ActionResult<ImportarNfeResponse>> Import(AppDbContext db, string content)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var file = new FormFile(stream, 0, stream.Length, "arquivo", "nota.xml");
            var controller = new EstoqueImportacaoController(db)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            return await controller.ImportarNfe(file, 1);
        }

        await using (var db = new AppDbContext(options, tenant))
        {
            var result = await Import(db, xml);
            Check(result.Result is CreatedResult { Value: ImportarNfeResponse }, "Importacao valida nao retornou 201.");
            db.ChangeTracker.Clear();
            var entrada = await db.EntradasCompra.Include(e => e.Fornecedor).Include(e => e.Itens).SingleAsync();
            Check(entrada.FornecedorId > 0 && entrada.Fornecedor?.CpfCnpj == "12345678000195", "Novo fornecedor nao foi vinculado a nota.");
            Check(entrada.Itens.Single().EntradaCompraId == entrada.Id && entrada.Itens.Single().CustoUnitario == 12.50m,
                "Itens ou custo nao foram persistidos corretamente.");
            Check(entrada.DataEmissao == new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc), "Data da nota incorreta.");
            Check((await Import(db, xml)).Result is ConflictObjectResult, "Aceitou NF-e duplicada.");
            Check(await db.Clientes.CountAsync() == 1 && await db.EntradasCompra.CountAsync() == 1, "Duplicidade criou registros extras.");
        }

        // Uma falha na gravacao da nota nao pode deixar um fornecedor salvo isoladamente.
        var failingOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"nfe-import-failure-{Guid.NewGuid()}")
            .AddInterceptors(new RejectPurchaseSave()).Options;
        await using (var db = new AppDbContext(failingOptions, tenant))
        {
            try
            {
                await Import(db, xml);
                throw new Exception("Falha de persistencia nao foi simulada.");
            }
            catch (PurchaseSaveException) { }
            db.ChangeTracker.Clear();
            Check(!await db.Clientes.AnyAsync() && !await db.EntradasCompra.AnyAsync() && !await db.EntradaCompraItens.AnyAsync() &&
                !await db.CadastroItens.AnyAsync() && !await db.Categorias.AnyAsync() && !await db.Marcas.AnyAsync(),
                "Falha na importacao deixou dados parcialmente salvos.");
        }

        await using (var db = new AppDbContext(options, tenant))
        {
            var fornecedor = await db.Clientes.SingleAsync();
            fornecedor.Relacionamento = RelacionamentoCadastro.Cliente;
            await db.SaveChangesAsync();
            var outraNota = xml.Replace(chave, new string('2', 44));
            Check((await Import(db, outraNota)).Result is CreatedResult, "Importacao com fornecedor existente falhou.");
            db.ChangeTracker.Clear();
            Check(await db.Clientes.CountAsync() == 1 && (await db.Clientes.SingleAsync()).Relacionamento == RelacionamentoCadastro.Ambos,
                "Fornecedor existente nao foi reutilizado ou atualizado.");
            Check((await Import(db, outraNota.Replace(new string('2', 44), new string('3', 44)).Replace("<mod>55</mod>", "<mod>65</mod>"))).Result is BadRequestObjectResult,
                "Aceitou documento de outro modelo.");
            Check((await Import(db, "<xml")).Result is BadRequestObjectResult, "Aceitou XML corrompido.");
        }
        await using (var db = new AppDbContext(options, tenant))
        {
            db.Categorias.Add(new Categoria { Id = 101, TenantId = 1, Nome = "Categoria" });
            db.Marcas.Add(new Marca { Id = 101, TenantId = 1, Nome = "Marca" });
            db.CadastroItens.Add(new CadastroItens { Id = 17, EmpresaId = 1, SKU = 1700, Descricao = "Peca", CategoriaId = 101, MarcaId = 101 });
            await db.SaveChangesAsync();
            var controller = new EstoqueImportacaoController(db);
            var lista = (OkObjectResult)(await controller.ListarEntradas(1)).Result!;
            Check(((IEnumerable<EntradaCompraResumoResponse>)lista.Value!).Count() == 2, "Notas importadas nao aparecem para retomada.");
            var id = await db.EntradasCompra.Select(e => e.Id).FirstAsync();
            var draft = new ConferirNfeRequest([new(1, null, 1.5m)]);
            Check((await controller.SalvarConferencia(id, draft, 1)).Result is OkObjectResult, "Nao salvou conferencia parcial.");
            db.ChangeTracker.Clear();
            var reopened = (ImportarNfeResponse)((OkObjectResult)(await controller.ObterEntrada(id, 1)).Result!).Value!;
            Check(reopened.Itens.Single().QuantidadeRecebida == 1.5m && reopened.Status == "Conferencia", "Reabertura perdeu conferencia salva.");
            Check((await controller.SalvarConferencia(id, draft with { Concluir = true }, 1)).Result is BadRequestObjectResult,
                "Concluiu conferencia com produto pendente.");
            Check((await controller.SalvarConferencia(id, new([new(1, 999, 2)]), 1)).Result is BadRequestObjectResult,
                "Aceitou produto nao cadastrado nesta empresa.");
            Check((await controller.SalvarConferencia(id, new([new(1, 17, -1)]), 1)).Result is BadRequestObjectResult,
                "Aceitou quantidade negativa.");
            Check((await controller.SalvarConferencia(id, new([new(1, 17, 1.12345m)]), 1)).Result is BadRequestObjectResult,
                "Aceitou quantidade que perderia precisao no banco.");
            Check((await controller.SalvarConferencia(id, new([]), 1)).Result is BadRequestObjectResult, "Aceitou lista incompleta.");
            Check((await controller.SalvarConferencia(id, new([new(1, 17, 1.5m)], true), 1)).Result is OkObjectResult,
                "Nao concluiu conferencia valida.");
            db.ChangeTracker.Clear();
            var saved = await db.EntradasCompra.Include(e => e.Itens).SingleAsync(e => e.Id == id);
            Check(saved.Status == "Conferida" && saved.Itens.Single().ProdutoId == 17 && saved.Itens.Single().QuantidadeRecebida == 1.5m,
                "Conferencia nao persistiu status, produto e quantidade.");
            Check((await controller.SalvarConferencia(id, draft, 1)).Result is ConflictObjectResult, "Permitiu alterar conferencia concluida.");
            Check(!await db.Estoques.AnyAsync(), "Conferencia criou movimento de estoque indevido.");

            var otherTenant = new TenantContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(2) } });
            await using var otherDb = new AppDbContext(options, otherTenant);
            var otherController = new EstoqueImportacaoController(otherDb);
            Check((await otherController.ObterEntrada(id, 2)).Result is NotFoundResult, "Leu nota de outra empresa.");
            Check((await otherController.SalvarConferencia(id, draft, 2)).Result is NotFoundResult, "Alterou conferencia de outra empresa.");
            var otherList = (OkObjectResult)(await otherController.ListarEntradas(2)).Result!;
            Check(!((IEnumerable<EntradaCompraResumoResponse>)otherList.Value!).Any(), "Listagem vazou notas entre empresas.");
        }
        Console.WriteLine("NF-e import and conference checks passed. Somente banco descartavel local.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class PurchaseSaveException : Exception { }

    private sealed class RejectPurchaseSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<EntradaCompra>().Any(e => e.State == EntityState.Added))
                throw new PurchaseSaveException();
            return ValueTask.FromResult(result);
        }
    }
}
