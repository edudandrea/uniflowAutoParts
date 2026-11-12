using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Services;

public sealed class NfeEstoqueSaidaService(AppDbContext db)
{
    public async Task SincronizarAsync(NotaFiscal nota, CancellationToken token)
    {
        if (nota.Situacao is not ("Autorizada" or "Cancelada")) return;
        await db.Entry(nota).Collection(n => n.Itens).LoadAsync(token);
        foreach (var item in nota.Itens)
        {
            var saida = await db.Estoques.SingleOrDefaultAsync(e => e.NotaFiscalItemId == item.Id && e.Tipo == "Saida", token);
            if (nota.Situacao == "Autorizada" && saida is null)
                db.Estoques.Add(Movimento(nota, item, "Saida"));
            if (nota.Situacao == "Cancelada" && saida is not null && !await db.Estoques.AnyAsync(e => e.NotaFiscalItemId == item.Id && e.Tipo == "Entrada", token))
                db.Estoques.Add(Movimento(nota, item, "Entrada"));
        }
    }
    private static Estoque Movimento(NotaFiscal nota, NotaFiscalItem item, string tipo) => new()
    {
        EmpresaId = nota.EmpresaId, ItemId = item.ProdutoId, NotaFiscalItemId = item.Id,
        Quantidade = item.Quantidade, CustoUnitario = item.CustoUnitario, PrecoVenda = (double)item.PrecoUnitario,
        Tipo = tipo, Documento = (tipo == "Entrada" ? "Cancelamento " : "") + nota.ChaveAcesso,
        Data = DateTime.UtcNow, UsuarioId = nota.UsuarioEmissorId
    };
}