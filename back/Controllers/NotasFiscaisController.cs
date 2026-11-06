using System.Security.Cryptography;
using System.Text;
using System.Xml;
using back.Contracts;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/notas-fiscais")]
public class NotasFiscaisController(AppDbContext db, SefazEventosService sefaz) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotaFiscalListaResponse>> Listar(
        [FromQuery] int empresaId, [FromQuery] int pagina = 1, [FromQuery] string? busca = null,
        [FromQuery] string? tipo = null, [FromQuery] string? situacao = null,
        [FromQuery] DateOnly? de = null, [FromQuery] DateOnly? ate = null,
        [FromQuery] string? serie = null, [FromQuery] int? ambiente = null,
        [FromQuery] decimal? valorMinimo = null, [FromQuery] decimal? valorMaximo = null,
        CancellationToken cancellationToken = default)
    {
        if (empresaId <= 0) return BadRequest("EmpresaId e obrigatorio.");
        if (de > ate || valorMinimo < 0 || valorMaximo < 0 || valorMinimo > valorMaximo)
            return BadRequest("Informe um periodo e um intervalo de valores validos.");
        if (tipo is not (null or "" or "NFe" or "NFCe") ||
            situacao is not (null or "" or "Autorizada" or "Processando" or "Cancelada" or "Rejeitada") ||
            ambiente is not (null or 1 or 2)) return BadRequest("Filtro fiscal invalido.");
        var query = db.NotasFiscais.AsNoTracking().Where(n => n.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var term = busca.Trim().ToLower();
            query = query.Where(n => n.Numero.Contains(term) || n.ChaveAcesso.Contains(term) ||
                n.Cliente.ToLower().Contains(term) || n.DocumentoCliente.Contains(term));
        }
        if (!string.IsNullOrEmpty(tipo)) query = query.Where(n => n.Tipo == tipo);
        if (!string.IsNullOrEmpty(situacao)) query = query.Where(n => n.Situacao == situacao);
        if (!string.IsNullOrWhiteSpace(serie)) query = query.Where(n => n.Serie == serie.Trim());
        if (ambiente.HasValue) query = query.Where(n => n.Ambiente == ambiente);
        if (valorMinimo.HasValue) query = query.Where(n => n.Valor >= valorMinimo);
        if (valorMaximo.HasValue) query = query.Where(n => n.Valor <= valorMaximo);
        // Datas do filtro representam o calendario de Sao Paulo (UTC-03).
        if (de.HasValue)
        {
            var inicio = DateTime.SpecifyKind(de.Value.ToDateTime(TimeOnly.MinValue).AddHours(3), DateTimeKind.Utc);
            query = query.Where(n => n.Emissao >= inicio);
        }
        if (ate.HasValue)
        {
            var fim = DateTime.SpecifyKind(ate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(3), DateTimeKind.Utc);
            query = query.Where(n => n.Emissao < fim);
        }
        var total = await query.CountAsync(cancellationToken);
        var resumo = new NotaFiscalResumo(total,
            await query.CountAsync(n => n.Situacao == "Autorizada", cancellationToken),
            await query.CountAsync(n => n.Situacao == "Processando", cancellationToken),
            await query.CountAsync(n => n.Situacao == "Cancelada", cancellationToken),
            await query.Where(n => n.Situacao == "Autorizada").SumAsync(n => n.Valor, cancellationToken));
        pagina = Math.Clamp(pagina, 1, Math.Max(1, (int)Math.Ceiling(total / 10d)));
        // Nao carrega XMLs, PDFs ou conteudo de eventos para montar a listagem.
        var notas = await query.OrderByDescending(n => n.Emissao).ThenByDescending(n => n.Id)
            .Skip((pagina - 1) * 10).Take(10)
            .Select(n => new
            {
                n.Id, n.Numero, n.Serie, n.Emissao, n.Cliente, n.DocumentoCliente, n.Tipo,
                n.Situacao, n.Valor, n.ChaveAcesso, n.Ambiente, TemPdf = n.Pdf != null,
                Pendente = n.Eventos.Any(e => e.Situacao == "Pendente" || e.Situacao == "Indeterminado"),
                Correcoes = n.Eventos.Count(e => e.Tipo == "110110" && e.Situacao == "Confirmado")
            }).ToListAsync(cancellationToken);
        var result = notas.Select(n =>
        {
            var bloqueio = n.Pendente ? "Existe evento sem confirmacao. Consulte a SEFAZ antes de enviar outro evento." :
                SefazEventosService.Bloqueio(new NotaFiscal { Ambiente = n.Ambiente, ChaveAcesso = n.ChaveAcesso, Situacao = n.Situacao });
            return new NotaFiscalResponse(n.Id, n.Numero, n.Serie, n.Emissao, n.Cliente, n.DocumentoCliente,
                n.Tipo, n.Situacao, n.Valor, n.ChaveAcesso, n.Ambiente, n.TemPdf,
                bloqueio is null, bloqueio is null && n.Tipo == "NFe" && n.Correcoes < 20, bloqueio);
        }).ToList();
        return Ok(new NotaFiscalListaResponse(pagina, 10, total, resumo, result));
    }

    [HttpPost("importar")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> Importar([FromForm] int empresaId, IFormFile xml, IFormFile? pdf, CancellationToken token)
    {
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, token);
        if (empresa is null) return BadRequest("Empresa nao encontrada.");
        if (xml.Length is 0 or > 5_000_000) return BadRequest("O XML deve ter ate 5 MB.");
        try
        {
            using var reader = new StreamReader(xml.OpenReadStream(), Encoding.UTF8, true);
            var nota = NotaFiscalXml.Importar(await reader.ReadToEndAsync(token), empresaId, empresa.Cnpj ?? "");
            if (await db.NotasFiscais.AnyAsync(n => n.EmpresaId == empresaId && n.ChaveAcesso == nota.ChaveAcesso && n.Ambiente == nota.Ambiente, token))
                return Conflict("Esta nota fiscal ja foi importada.");
            if (pdf is not null)
            {
                if (pdf.Length is 0 or > 5_000_000) return BadRequest("O PDF deve ter ate 5 MB.");
                await using var buffer = new MemoryStream();
                await pdf.CopyToAsync(buffer, token);
                nota.Pdf = buffer.ToArray();
                if (!nota.Pdf.AsSpan().StartsWith("%PDF-"u8)) return BadRequest("Arquivo PDF invalido.");
            }
            db.NotasFiscais.Add(nota);
            await db.SaveChangesAsync(token);
            return Ok(new { nota.Id });
        }
        catch (Exception error) when (error is ArgumentException or XmlException or InvalidOperationException)
        {
            return BadRequest(error is XmlException ? "XML invalido." : error.Message);
        }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return Conflict("Esta nota fiscal ja foi importada.");
        }
    }

    [HttpGet("{id:long}/{formato}")]
    public async Task<IActionResult> Baixar(long id, string formato, [FromQuery] int empresaId, CancellationToken token)
    {
        if (formato is not ("pdf" or "xml")) return NotFound();
        var nota = await db.NotasFiscais.AsNoTracking().Where(n => n.Id == id && n.EmpresaId == empresaId)
            .Select(n => new { n.Numero, n.Serie, n.Xml, n.Pdf }).FirstOrDefaultAsync(token);
        if (nota is null) return NotFound("Nota fiscal nao encontrada.");
        var nome = $"nota-{nota.Numero}-{nota.Serie}";
        if (formato == "xml") return File(Encoding.UTF8.GetBytes(nota.Xml), "application/xml", nome + ".xml");
        return nota.Pdf is null ? NotFound("O PDF desta nota ainda nao foi anexado.") : File(nota.Pdf, "application/pdf", nome + ".pdf");
    }

    [HttpPost("{id:long}/cancelar")]
    [RequestSizeLimit(1_500_000)]
    public Task<IActionResult> Cancelar(long id, [FromQuery] int empresaId, NotaFiscalEventoRequest request, CancellationToken token)
        => RegistrarEvento(id, empresaId, "110111", request, token);

    [HttpPost("{id:long}/consultar")]
    [RequestSizeLimit(1_500_000)]
    public async Task<IActionResult> Consultar(long id, [FromQuery] int empresaId, CertificadoRequest request, CancellationToken token)
    {
        if (id <= 0 || empresaId <= 0) return BadRequest("Nota e empresa sao obrigatorias.");
        await db.Database.OpenConnectionAsync(token);
        var locked = false;
        try
        {
            locked = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({-id}) AS \"Value\"").SingleAsync(token);
            if (!locked) return Conflict("Ja existe uma operacao em andamento nesta nota fiscal.");
            var nota = await db.NotasFiscais.Include(n => n.Eventos).FirstOrDefaultAsync(n => n.Id == id && n.EmpresaId == empresaId, token);
            if (nota is null) return NotFound();
            var bloqueio = SefazEventosService.Bloqueio(new NotaFiscal { ChaveAcesso = nota.ChaveAcesso, Ambiente = nota.Ambiente, Situacao = "Autorizada" });
            if (bloqueio is not null) return Conflict(bloqueio);
            using var certificate = SefazEventosService.CarregarCertificado(request.Base64, request.Password);
            var retorno = await sefaz.ConsultarAsync(nota, certificate, token);
            var ns = NotaFiscalXml.Ns;
            var codigo = retorno.Element(ns + "cStat")?.Value;
            if (codigo is "100" or "150") nota.Situacao = "Autorizada";
            else if (codigo is "101" or "151" or "155") nota.Situacao = "Cancelada";
            else return Problem(statusCode: 422, detail: $"SEFAZ {codigo}: {retorno.Element(ns + "xMotivo")?.Value}");
            foreach (var proc in retorno.Elements(ns + "procEventoNFe"))
            {
                var envio = proc.Element(ns + "evento")?.Element(ns + "infEvento");
                var info = proc.Element(ns + "retEvento")?.Element(ns + "infEvento");
                if (info?.Element(ns + "cStat")?.Value != "135" || info.Element(ns + "chNFe")?.Value != nota.ChaveAcesso
                    || info.Element(ns + "tpAmb")?.Value != "2" || string.IsNullOrWhiteSpace(info.Element(ns + "nProt")?.Value)) continue;
                var tipo = info.Element(ns + "tpEvento")?.Value;
                if (tipo is not ("110110" or "110111") || !int.TryParse(info.Element(ns + "nSeqEvento")?.Value, out var seq)) continue;
                var evento = nota.Eventos.FirstOrDefault(e => e.Tipo == tipo && e.Sequencia == seq && e.Situacao != "Rejeitado");
                if (evento is null)
                {
                    evento = new NotaFiscalEvento { Tipo = tipo, Sequencia = seq };
                    nota.Eventos.Add(evento);
                }
                evento.Codigo = "135";
                evento.Protocolo = info.Element(ns + "nProt")!.Value;
                evento.Motivo = info.Element(ns + "xMotivo")?.Value ?? "Evento confirmado.";
                evento.Situacao = "Confirmado";
                evento.XmlRetorno = proc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
                evento.Texto = envio?.Element(ns + "detEvento")?.Element(ns + (tipo == "110110" ? "xCorrecao" : "xJust"))?.Value ?? evento.Texto;
            }
            await db.SaveChangesAsync(CancellationToken.None);
            return Ok(new { codigo, motivo = retorno.Element(ns + "xMotivo")?.Value, nota.Situacao,
                pendente = nota.Eventos.Any(e => e.Situacao is "Pendente" or "Indeterminado") });
        }
        catch (Exception error) when (error is ArgumentException or CryptographicException)
        {
            return Problem(statusCode: 400, detail: error is CryptographicException ? "Certificado A1 invalido ou senha incorreta." : error.Message);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or XmlException)
        {
            return Problem(statusCode: 502, detail: "Nao foi possivel consultar a situacao na SEFAZ. Tente novamente.");
        }
        finally
        {
            try { if (locked) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({-id})", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    [HttpPost("{id:long}/carta-correcao")]
    [RequestSizeLimit(1_500_000)]
    public Task<IActionResult> Corrigir(long id, [FromQuery] int empresaId, NotaFiscalEventoRequest request, CancellationToken token)
        => RegistrarEvento(id, empresaId, "110110", request, token);

    private async Task<IActionResult> RegistrarEvento(long id, int empresaId, string tipo, NotaFiscalEventoRequest request, CancellationToken token)
    {
        if (id <= 0 || empresaId <= 0) return BadRequest("Nota e empresa sao obrigatorias.");
        await db.Database.OpenConnectionAsync(token);
        var locked = false;
        try
        {
            // Lock de sessao impede transmissao simultanea mesmo em replicas distintas.
            locked = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({-id}) AS \"Value\"").SingleAsync(token);
            if (!locked) return Conflict("Ja existe uma operacao em andamento nesta nota fiscal.");
            var nota = await db.NotasFiscais.Include(n => n.Eventos).FirstOrDefaultAsync(n => n.Id == id && n.EmpresaId == empresaId, token);
            if (nota is null) return NotFound("Nota fiscal nao encontrada.");
            var bloqueio = SefazEventosService.Bloqueio(nota);
            if (bloqueio is not null) return Conflict(bloqueio);
            if (nota.Eventos.Any(e => e.Situacao is "Pendente" or "Indeterminado"))
                return Conflict("Existe evento sem confirmacao. Consulte a SEFAZ antes de enviar outro evento.");
            using var certificate = SefazEventosService.CarregarCertificado(request.Base64, request.Password);
            var sequencia = nota.Eventos.Where(e => e.Tipo == tipo && e.Situacao == "Confirmado").Select(e => e.Sequencia).DefaultIfEmpty(0).Max() + 1;
            var evento = new NotaFiscalEvento
            {
                NotaFiscalId = nota.Id, Tipo = tipo, Sequencia = sequencia, Texto = request.Texto?.Trim() ?? "",
            };
            evento.XmlEnvio = SefazEventosService.CriarEvento(nota, tipo, sequencia, evento.Texto, certificate);
            db.NotaFiscalEventos.Add(evento);
            // Grava antes de transmitir: timeout ou queda nao liberam um reenvio cego.
            await db.SaveChangesAsync(token);
            try
            {
                var result = await sefaz.EnviarAsync(nota, evento, certificate, token);
                evento.Codigo = result.Codigo;
                evento.Motivo = result.Motivo;
                evento.Protocolo = result.Protocolo;
                evento.XmlRetorno = result.Xml;
                evento.Situacao = result.Confirmado ? "Confirmado" :
                    int.TryParse(result.Codigo, out var codigo) && codigo >= 200 && codigo != 573 ? "Rejeitado" : "Indeterminado";
                if (result.Confirmado && tipo == "110111") nota.Situacao = "Cancelada";
                await db.SaveChangesAsync(CancellationToken.None);
                if (!result.Confirmado)
                    return Problem(statusCode: 422, detail: $"SEFAZ {result.Codigo}: {result.Motivo}");
                return Ok(new NotaFiscalEventoResponse(result.Codigo, result.Motivo, result.Protocolo, sequencia));
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or XmlException)
            {
                evento.Situacao = "Indeterminado";
                evento.Motivo = "Sem confirmacao de processamento. Consulte a SEFAZ antes de reenviar.";
                await db.SaveChangesAsync(CancellationToken.None);
                return Problem(statusCode: 502, detail: evento.Motivo);
            }
        }
        catch (Exception error) when (error is ArgumentException or CryptographicException)
        {
            return Problem(statusCode: 400, detail: error is CryptographicException ? "Certificado A1 invalido ou senha incorreta." : error.Message);
        }
        finally
        {
            try
            {
                if (locked) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({-id})", CancellationToken.None);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
