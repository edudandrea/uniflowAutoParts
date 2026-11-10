using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Services;

public sealed class NfeEstoqueService(AppDbContext db)
{
    // Atualiza o movimento do item, em vez de acrescentar outra entrada a cada importacao.
    // O chamador salva nota, produtos e movimentos numa unica transacao implicita.
    public async Task SincronizarAsync(EntradaCompra entrada)
    {
        if (db.CurrentUsuarioId <= 0) throw new TenantAccessException();
        db.ChangeTracker.DetectChanges();
        var ids = entrada.Itens.Select(i => i.Id).ToArray();
        var movimentos = await db.Estoques.Where(m => m.EmpresaId == entrada.TenantId &&
            m.EntradaCompraItemId.HasValue && ids.Contains(m.EntradaCompraItemId.Value))
            .ToDictionaryAsync(m => m.EntradaCompraItemId!.Value);
        var alterado = false;
        foreach (var item in entrada.Itens)
        {
            movimentos.TryGetValue(item.Id, out var movimento);
            if ((item.ProdutoId is null && item.Produto is null) || item.QuantidadeRecebida == 0)
            {
                if (movimento is not null) { db.Estoques.Remove(movimento); alterado = true; }
                continue;
            }
            var produto = item.Produto ?? await db.CadastroItens.SingleAsync(p => p.Id == item.ProdutoId && p.EmpresaId == entrada.TenantId);
            if (movimento is not null && movimento.ItemId == produto.Id && movimento.Quantidade == item.QuantidadeRecebida &&
                movimento.CustoUnitario == (double)item.CustoUnitario) continue;
            if (movimento is null)
            {
                movimento = new Estoque
                {
                    EmpresaId = checked((int)entrada.TenantId), EntradaCompraItem = item,
                    Tipo = "Entrada", Documento = entrada.ChaveNfe, Data = DateTime.UtcNow,
                    UsuarioId = db.CurrentUsuarioId
                };
                db.Estoques.Add(movimento);
            }
            movimento.Produto = produto;
            movimento.Quantidade = item.QuantidadeRecebida;
            movimento.CustoUnitario = (double)item.CustoUnitario;
            alterado = true;
        }
        if (alterado) entrada.VersaoEstoque++;
    }
}
