using back.Data;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/financeiro/contas-a-receber")]
public sealed class ContasReceberController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Listar([FromQuery] int empresaId, [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate, CancellationToken token)
    {
        if (empresaId <= 0 || de > ate)
            return BadRequest("Informe um período de vencimentos válido.");

        var notas = db.NotasFiscais.AsNoTracking()
            .Where(n => n.EmpresaId == empresaId && n.Tipo == "NFe" && n.Ambiente == 1
                && n.Situacao == "Autorizada" && n.Xml.Contains("dVenc"))
            .OrderBy(n => n.Id)
            .Select(n => new { n.Id, n.Numero, n.Serie, n.Cliente, n.DocumentoCliente, n.ChaveAcesso, n.Xml });

        var titulos = new List<TituloVenda>();
        var clientes = new Dictionary<string, ClienteFiltro>(StringComparer.OrdinalIgnoreCase);
        var naturezas = new HashSet<string>();
        await foreach (var nota in notas.AsAsyncEnumerable().WithCancellation(token))
        {
            var cobranca = NotaFiscalXml.LerCobranca(nota.Xml, nota.ChaveAcesso);
            if (cobranca is null || cobranca.Parcelas.Count == 0) continue;

            var nome = nota.Cliente.Trim();
            long? clienteId = null;
            if (nome.Length > 0)
            {
                var documento = new string(nota.DocumentoCliente.Where(char.IsAsciiDigit).ToArray());
                var chaveCliente = documento.Length is 11 or 14 ? "doc:" + documento : "nome:" + nome;
                clientes.TryAdd(chaveCliente, new(nota.Id, nome));
                clienteId = clientes[chaveCliente].Id;
            }
            if (cobranca.Natureza.Length > 0) naturezas.Add(cobranca.Natureza);
            foreach (var parcela in cobranca.Parcelas)
            {
                if (de > parcela.Vencimento || ate < parcela.Vencimento) continue;
                titulos.Add(new(nota.Id, nota.Numero, nota.Serie, clienteId, nome, cobranca.Natureza,
                    parcela.Indice, parcela.Numero, parcela.Vencimento, parcela.Valor));
            }
        }

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        return Ok(new { Hoje = hoje, Titulos = titulos, Clientes = clientes.Values, Naturezas = naturezas });
    }

    private sealed record ClienteFiltro(long Id, string Nome);
    private sealed record TituloVenda(long NotaId, string Nota, string Serie, long? ClienteId, string Cliente,
        string Natureza, int Indice, string? Parcela, DateOnly Vencimento, decimal Valor);
}
