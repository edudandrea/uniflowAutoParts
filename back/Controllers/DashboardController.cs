using back.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromQuery] int empresaId, [FromQuery] DateOnly de,
        [FromQuery] DateOnly ate, CancellationToken token)
    {
        if (empresaId <= 0 || de.Year < 2000 || ate.Year > 2100 || ate < de || ate.DayNumber - de.DayNumber > 31)
            return BadRequest("Informe um período de até 32 dias.");

        // O calendário do painel segue São Paulo, assim como a tela de notas.
        var inicio = InicioUtc(de);
        var fim = InicioUtc(ate.AddDays(1));
        var producao = db.NotasFiscais.AsNoTracking().Where(n => n.EmpresaId == empresaId && n.Ambiente == 1);
        var periodo = producao.Where(n => n.Emissao >= inicio && n.Emissao < fim);
        var autorizadas = periodo.Where(n => n.Situacao == "Autorizada");

        var serie = await autorizadas.GroupBy(n => n.Emissao.AddHours(-3).Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Dia = g.Key, Valor = g.Sum(n => n.Valor), Quantidade = g.Count() })
            .ToListAsync(token);

        var itens = db.NotaFiscalItens.AsNoTracking().Where(i => autorizadas.Any(n => n.Id == i.NotaFiscalId));
        var produtos = await itens.Where(i => i.Produto.EmpresaId == empresaId)
            .GroupBy(i => new { i.ProdutoId, i.Produto.Descricao, i.Produto.SKU })
            .Select(g => new { Id = g.Key.ProdutoId, g.Key.Descricao, g.Key.SKU, Quantidade = g.Sum(i => i.Quantidade) })
            .OrderByDescending(p => p.Quantidade).ThenBy(p => p.Id).Take(5).ToListAsync(token);

        var valores = await itens.GroupBy(_ => 1)
            .Select(g => new
            {
                Notas = g.Select(i => i.NotaFiscalId).Distinct().Count(),
                SemCusto = g.Count(i => i.CustoUnitario <= 0),
                Custo = g.Sum(i => i.CustoUnitario * (double)i.Quantidade)
            }).SingleOrDefaultAsync(token);
        var faturamento = serie.Sum(d => d.Valor);
        var total = serie.Sum(d => d.Quantidade);
        var anterior = inicio.AddDays(de.DayNumber - ate.DayNumber - 1);
        var comparacao = await producao
            .Where(n => n.Situacao == "Autorizada" && n.Emissao >= anterior && n.Emissao < inicio)
            .GroupBy(_ => 1).Select(g => new { Valor = g.Sum(n => n.Valor), Quantidade = g.Count() })
            .SingleOrDefaultAsync(token);
        // Notas importadas sem itens ou itens sem custo não permitem calcular a margem do período.
        double? margem = faturamento > 0 && valores is { SemCusto: 0 } && valores.Notas == total
            ? (1 - valores.Custo / (double)faturamento) * 100
            : null;

        var notas = await periodo.OrderByDescending(n => n.Emissao).ThenByDescending(n => n.Id).Take(5)
            .Select(n => new { n.Numero, n.Emissao, n.Cliente, n.Situacao, n.Valor, n.Tipo }).ToListAsync(token);
        var entradas = await db.EntradasCompra.AsNoTracking()
            .Where(e => e.TenantId == empresaId && e.ImportadoEm >= inicio && e.ImportadoEm < fim)
            .OrderByDescending(e => e.ImportadoEm).ThenByDescending(e => e.Id).Take(5)
            .Select(e => new { e.NumeroNfe, e.ImportadoEm, e.Status }).ToListAsync(token);

        var pendentes = await producao.CountAsync(n => n.Situacao == "Processando", token);
        var rejeitadas = await periodo.CountAsync(n => n.Situacao == "Rejeitada", token);
        var saldos = db.SaldosEstoque(empresaId);
        var estoqueBaixo = await (
            from p in db.CadastroItens.AsNoTracking()
            where p.EmpresaId == empresaId && p.Ativo && p.EstoqueMinimo > 0
            join s in saldos on p.Id equals s.ItemId into saldosProduto
            from s in saldosProduto.DefaultIfEmpty()
            where ((decimal?)s.Quantidade ?? 0) < p.EstoqueMinimo
            select p.Id).CountAsync(token);

        return Ok(new { Faturamento = faturamento, Total = total, Margem = margem, Serie = serie,
            Produtos = produtos, Notas = notas, Entradas = entradas, Pendentes = pendentes,
            Rejeitadas = rejeitadas, EstoqueBaixo = estoqueBaixo, Comparacao = comparacao });
    }

    private static DateTime InicioUtc(DateOnly dia) =>
        DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue).AddHours(3), DateTimeKind.Utc);
}
