using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Services;

public sealed class NfeEstoqueSaidaService(AppDbContext db)
{
    public async Task SincronizarAsync(NotaFiscal nota, CancellationToken token)
    {
        // Rascunhos não têm nota persistida. A venda registrada já baixa mesmo pendente/rejeitada.
        if (nota.Situacao is not ("Autorizada" or "Cancelada") && (nota.SolicitacaoId is null || nota.XmlEnvioNfe.Length == 0)) return;
        var nova = db.Entry(nota).State == EntityState.Added;
        if (!nova) await db.Entry(nota).Collection(n => n.Itens).LoadAsync(token);
        var origem = nota.VendaOrigemId ?? nota.Id;
        var grupo = db.NotasFiscais.Where(n => n.EmpresaId == nota.EmpresaId && (n.Id == origem || n.VendaOrigemId == origem));
        if (!nova && await grupo.AnyAsync(n => n.Id > nota.Id, token)) return;
        var notasIds = await grupo.Select(n => n.Id).ToListAsync(token);
        var itens = await db.NotaFiscalItens.Where(i => notasIds.Contains(i.NotaFiscalId)).ToListAsync(token);
        var ids = itens.Select(i => i.Id).ToArray();
        if (nova) itens.AddRange(nota.Itens);
        var movimentos = await db.Estoques.Where(e => e.EmpresaId == nota.EmpresaId && e.NotaFiscalItemId != null && ids.Contains(e.NotaFiscalItemId.Value)).ToListAsync(token);
        foreach (var produto in itens.Select(i => i.ProdutoId).Distinct())
        {
            var itensProduto = itens.Where(i => i.ProdutoId == produto).ToList();
            var itemIds = itensProduto.Select(i => (long?)i.Id).ToHashSet();
            var saldoBaixado = movimentos.Where(e => itemIds.Contains(e.NotaFiscalItemId)).Sum(e => e.Tipo == "Saida" ? e.Quantidade : -e.Quantidade);
            var desejado = nota.Situacao == "Cancelada" ? 0 : nota.Itens.Where(i => i.ProdutoId == produto).Sum(i => i.Quantidade);
            var diferenca = desejado - saldoBaixado;
            if (diferenca == 0) continue;
            var item = nota.Itens.FirstOrDefault(i => i.ProdutoId == produto) ?? itensProduto.Last();
            var tipo = diferenca > 0 ? "Saida" : "Entrada";
            var movimento = movimentos.SingleOrDefault(e => e.NotaFiscalItemId == item.Id && e.Tipo == tipo);
            if (movimento is not null) movimento.Quantidade += Math.Abs(diferenca);
            else
            {
                movimento = new Estoque {
                    EmpresaId = nota.EmpresaId, ItemId = produto, NotaFiscalItem = item,
                    Quantidade = Math.Abs(diferenca), CustoUnitario = item.CustoUnitario, PrecoVenda = (double)item.PrecoUnitario,
                    Tipo = tipo, Documento = (nota.Situacao == "Cancelada" ? "Cancelamento " : nota.VendaOrigemId is not null ? "Revisao da venda " : "Venda ") + nota.ChaveAcesso,
                    Data = DateTime.UtcNow, UsuarioId = nota.UsuarioEmissorId
                };
                movimentos.Add(movimento); db.Estoques.Add(movimento);
            }
        }
    }
}
