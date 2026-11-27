using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/financeiro")]
public sealed class FinanceiroController(AppDbContext db) : ControllerBase
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    [HttpGet("resumo")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromQuery] int empresaId, [FromQuery] DateOnly mes, CancellationToken token)
    {
        if (empresaId <= 0 || mes.Year is < 2000 or > 2100 || mes.Day != 1)
            return BadRequest("Informe o primeiro dia de um mês válido.");

        var inicio = InicioUtc(mes.AddMonths(-5));
        var inicioMes = InicioUtc(mes);
        var fim = InicioUtc(mes.AddMonths(1));

        var saidas = SaidasAutorizadas(empresaId, inicio, fim);
        var compras = ComprasConferidas(empresaId, inicio, fim);

        var saidasPorMes = await saidas.GroupBy(n => new
            { Ano = n.Emissao.AddHours(-3).Year, Mes = n.Emissao.AddHours(-3).Month })
            .Select(g => new { g.Key.Ano, g.Key.Mes, Valor = g.Sum(n => n.Valor), Quantidade = g.Count() })
            .ToListAsync(token);
        var comprasPorMes = await compras.GroupBy(e => new
            { Ano = e.DataEmissao!.Value.AddHours(-3).Year, Mes = e.DataEmissao!.Value.AddHours(-3).Month })
            .Select(g => new { g.Key.Ano, g.Key.Mes, Valor = g.Sum(e => e.ValorTotal), Quantidade = g.Count() })
            .ToListAsync(token);

        var serie = Enumerable.Range(-5, 6).Select(offset =>
        {
            var competencia = mes.AddMonths(offset);
            var saida = saidasPorMes.SingleOrDefault(s => s.Ano == competencia.Year && s.Mes == competencia.Month);
            var compra = comprasPorMes.SingleOrDefault(c => c.Ano == competencia.Year && c.Mes == competencia.Month);
            return new
            {
                Mes = competencia,
                Saidas = saida?.Valor ?? 0,
                TotalSaidas = saida?.Quantidade ?? 0,
                Compras = compra?.Valor ?? 0,
                TotalCompras = compra?.Quantidade ?? 0,
            };
        }).ToArray();

        var notasRecentes = await saidas.Where(n => n.Emissao >= inicioMes)
            .OrderByDescending(n => n.Emissao).ThenByDescending(n => n.Id).Take(5)
            .Select(n => new DocumentoResumo(n.Id, "saida", n.Tipo, n.Numero, n.Serie, n.Emissao, n.Cliente, n.Valor))
            .ToListAsync(token);
        var comprasRecentes = await compras.Where(e => e.DataEmissao >= inicioMes)
            .OrderByDescending(e => e.DataEmissao).ThenByDescending(e => e.Id).Take(5)
            .Select(e => new DocumentoResumo(e.Id, "compra", "NFe", e.NumeroNfe, e.Serie, e.DataEmissao!.Value,
                e.Fornecedor != null ? e.Fornecedor.NomeRazaoSocial : null, e.ValorTotal))
            .ToListAsync(token);

        var documentos = notasRecentes.Concat(comprasRecentes).OrderByDescending(d => d.Emissao)
            .ThenByDescending(d => d.Id).Take(5);
        return Ok(new { Serie = serie, Documentos = documentos });
    }

    [HttpGet("movimentacoes/referencia-fiscal")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Movimentacoes([FromQuery] int empresaId, [FromQuery] DateOnly de,
        [FromQuery] DateOnly ate, CancellationToken token)
    {
        if (!PeriodoValido(empresaId, de, ate))
            return BadRequest("Informe um período de até 366 dias.");

        var inicio = InicioDiaUtc(de);
        var fim = InicioDiaUtc(ate.AddDays(1));
        var documentos = await SaidasAutorizadas(empresaId, inicio, fim)
            .Select(n => new DocumentoResumo(n.Id, "saida", n.Tipo, n.Numero, n.Serie, n.Emissao, n.Cliente, n.Valor))
            .ToListAsync(token);
        var compras = ComprasConferidas(empresaId, inicio, fim)
            .Select(e => new { e.Id, e.NumeroNfe, e.Serie, Emissao = e.DataEmissao!.Value, e.ChaveNfe, e.XmlOriginal,
                Nome = e.Fornecedor != null ? e.Fornecedor.NomeRazaoSocial : null, e.ValorTotal });
        await foreach (var compra in compras.AsAsyncEnumerable().WithCancellation(token))
        {
            if (NotaFiscalXml.TemProtocoloProducao(compra.XmlOriginal, compra.ChaveNfe))
                documentos.Add(new(compra.Id, "compra", "NFe", compra.NumeroNfe, compra.Serie, compra.Emissao,
                    compra.Nome, compra.ValorTotal));
        }

        return Ok(new { Documentos = documentos.OrderByDescending(d => d.Emissao)
            .ThenBy(d => d.Origem).ThenByDescending(d => d.Id) });
    }

    [HttpGet("caixa/referencia-fiscal")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Caixa([FromQuery] int empresaId, [FromQuery] DateOnly de,
        [FromQuery] DateOnly ate, CancellationToken token)
    {
        if (!PeriodoValido(empresaId, de, ate))
            return BadRequest("Informe um período de até 366 dias.");

        var inicio = InicioDiaUtc(de);
        var fim = InicioDiaUtc(ate.AddDays(1));
        var saidas = SaidasAutorizadas(empresaId, inicio, fim).Where(n => n.Xml.Contains("detPag"))
            .Select(n => new DocumentoComXml(n.Id, n.Tipo, n.Numero, n.Serie, n.Emissao, n.Cliente, n.ChaveAcesso, n.Xml));
        var compras = ComprasConferidas(empresaId, inicio, fim).Where(n => n.XmlOriginal.Contains("detPag"))
            .Select(n => new DocumentoComXml(n.Id, "NFe", n.NumeroNfe, n.Serie, n.DataEmissao!.Value,
                n.Fornecedor != null ? n.Fornecedor.NomeRazaoSocial : null, n.ChaveNfe, n.XmlOriginal));

        var documentos = new List<DocumentoCaixa>();
        await AdicionarDocumentos(saidas, "saida");
        await AdicionarDocumentos(compras, "compra");
        return Ok(new { Documentos = documentos.OrderByDescending(d => d.Emissao)
            .ThenBy(d => d.Origem).ThenByDescending(d => d.Id) });

        async Task AdicionarDocumentos(IQueryable<DocumentoComXml> consulta, string origem)
        {
            await foreach (var nota in consulta.AsAsyncEnumerable().WithCancellation(token))
            {
                var valor = NotaFiscalXml.LerDinheiroDeclarado(nota.Xml, nota.Chave);
                if (valor > 0)
                    documentos.Add(new(nota.Id, origem, nota.Tipo, nota.Numero, nota.Serie, nota.Emissao,
                        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nota.Emissao, Zona)), nota.Nome, valor));
            }
        }
    }

    private IQueryable<NotaFiscal> SaidasAutorizadas(int empresaId, DateTime inicio, DateTime fim) =>
        db.NotasFiscais.AsNoTracking().Where(n => n.EmpresaId == empresaId && n.Ambiente == 1
            && n.Situacao == "Autorizada" && n.Emissao >= inicio && n.Emissao < fim);

    private IQueryable<EntradaCompra> ComprasConferidas(int empresaId, DateTime inicio, DateTime fim) =>
        db.EntradasCompra.AsNoTracking().Where(e => e.TenantId == empresaId && e.Status == "Conferida"
            && e.DataEmissao >= inicio && e.DataEmissao < fim);

    private static bool PeriodoValido(int empresaId, DateOnly de, DateOnly ate) =>
        empresaId > 0 && de.Year >= 2000 && ate.Year <= 2100 && ate >= de && ate.DayNumber - de.DayNumber < 366;

    private static DateTime InicioDiaUtc(DateOnly dia)
    {
        var inicioLocal = dia.ToDateTime(TimeOnly.MinValue);
        // No início do horário de verão, o relógio local saltava da meia-noite para a 1h.
        if (Zona.IsInvalidTime(inicioLocal)) inicioLocal = inicioLocal.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Zona);
    }

    // Mesmo calendário (UTC-03:00) do painel principal.
    private static DateTime InicioUtc(DateOnly dia) =>
        dia.ToDateTime(new TimeOnly(3, 0), DateTimeKind.Utc);

    private sealed record DocumentoResumo(long Id, string Origem, string Tipo, string Numero, string Serie,
        DateTime Emissao, string? Nome, decimal Valor);
    private sealed record DocumentoComXml(long Id, string Tipo, string Numero, string Serie, DateTime Emissao,
        string? Nome, string Chave, string Xml);
    private sealed record DocumentoCaixa(long Id, string Origem, string Tipo, string Numero, string Serie,
        DateTime Emissao, DateOnly Dia, string? Nome, decimal Valor);
}
