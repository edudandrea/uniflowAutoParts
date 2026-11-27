using back.Data;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/financeiro/contas-a-pagar")]
public sealed class ContasPagarController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Listar([FromQuery] int empresaId, [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate, CancellationToken token)
    {
        if (empresaId <= 0 || de > ate)
            return BadRequest("Informe um período de vencimentos válido.");

        var notas = db.EntradasCompra.AsNoTracking()
            .Where(n => n.TenantId == empresaId && n.Status == "Conferida" && n.XmlOriginal.Contains("dVenc"))
            .Select(n => new { n.Id, n.NumeroNfe, n.Serie, n.ChaveNfe, n.FornecedorId, n.NaturezaOperacao,
                Fornecedor = n.Fornecedor == null ? null : n.Fornecedor.NomeRazaoSocial, n.XmlOriginal });
        var titulos = new List<TituloCompra>();
        var fornecedores = new Dictionary<long, string>();
        var naturezas = new HashSet<string>();
        await foreach (var nota in notas.AsAsyncEnumerable().WithCancellation(token))
        {
            foreach (var parcela in NotaFiscalXml.LerCobranca(nota.XmlOriginal, nota.ChaveNfe)?.Parcelas ?? [])
            {
                if (nota.FornecedorId is long id && !string.IsNullOrWhiteSpace(nota.Fornecedor))
                    fornecedores.TryAdd(id, nota.Fornecedor);
                if (!string.IsNullOrWhiteSpace(nota.NaturezaOperacao))
                    naturezas.Add(nota.NaturezaOperacao);
                if (de > parcela.Vencimento || ate < parcela.Vencimento) continue;
                titulos.Add(new(nota.Id, nota.NumeroNfe, nota.Serie, nota.FornecedorId, nota.Fornecedor,
                    nota.NaturezaOperacao, parcela.Indice, parcela.Numero, parcela.Vencimento, parcela.Valor));
            }
        }

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        return Ok(new { Hoje = hoje, Titulos = titulos,
            Fornecedores = fornecedores.Select(f => new { Id = f.Key, Nome = f.Value }), Naturezas = naturezas });
    }

    private sealed record TituloCompra(long EntradaId, string Nota, string Serie, long? FornecedorId, string? Fornecedor,
        string Natureza, int Indice, string? Parcela, DateOnly Vencimento, decimal Valor);
}
