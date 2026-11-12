using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Schema;
using back.Contracts;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/notas-fiscais")]
public sealed class EmissaoNfeController(AppDbContext db, TenantContext tenant, NfeAutorizacaoService sefaz, SefazEventosService eventos) : ControllerBase
{
    [HttpPost("emitir")]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> Emitir(EmitirNfeRequest request, CancellationToken token)
    {
        if (tenant.EmpresaId <= 0) return Unauthorized();
        await using var gate = await FiscalLock.AdquirirAsync(db, tenant.EmpresaId, token);
        if (gate is null) return Conflict("Existe uma emissao em andamento para esta empresa.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { request.ClienteId, request.Ambiente, request.Serie, request.Natureza, request.Cfop, request.Modelo, request.Desconto, request.Frete, request.Despesas, request.Observacoes, request.Itens, request.Pagamentos, request.Destinatario, request.ModalidadeFrete, request.IndicadorPresenca }))));
        var existing = await db.NotasFiscais.SingleOrDefaultAsync(n => n.EmpresaId == tenant.EmpresaId && n.SolicitacaoId == request.SolicitacaoId, token);
        if (existing is not null)
            return existing.HashSolicitacao == hash ? Ok(ToResponse(existing)) : Conflict("Esta solicitacao ja esta vinculada a outra nota. Consulte a nota antes de alterar a venda.");
        try
        {
            var empresa = await db.Empresas.SingleAsync(e => e.Id == tenant.EmpresaId, token);
            var config = await db.ConfiguracoesFiscais.SingleOrDefaultAsync(c => c.EmpresaId == tenant.EmpresaId, token) ?? new ConfiguracaoFiscal { EmpresaId = tenant.EmpresaId };
            var tax = await db.ConfiguracoesTributarias.SingleOrDefaultAsync(c => c.EmpresaId == tenant.EmpresaId, token);
            if (tax is null) return BadRequest("Configure a tributacao da empresa antes de emitir.");
            var cliente = await db.Clientes.Include(c => c.CadastroEndereco).Include(c => c.Enderecos).SingleOrDefaultAsync(c => c.Id == request.ClienteId && c.TenantId == tenant.EmpresaId, token);
            if (cliente is null) return BadRequest("Cliente nao encontrado na empresa.");
            if (request.Itens is null) return BadRequest("Informe os itens da venda.");
            var ids = request.Itens.Select(i => i.ProdutoId).ToArray();
            var produtos = await db.CadastroItens.Where(p => p.EmpresaId == tenant.EmpresaId && ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, token);
            var numeracao = await db.NumeracoesNfe.SingleOrDefaultAsync(n => n.EmpresaId == tenant.EmpresaId && n.Ambiente == config.Ambiente && n.Serie == request.Serie, token);
            var numero = Math.Max(numeracao?.ProximoNumero ?? 1, await EmitenteFiscalController.ProximoLivre(db, tenant.EmpresaId, config.Ambiente, request.Serie, token));
            using var cert = SefazEventosService.CarregarCertificado(request.Base64, request.Password);
            var nota = NfeGerador.Criar(empresa, config, tax, cliente, produtos, request, numero, tenant.UsuarioId, cert);
            nota.HashSolicitacao = hash;
            db.NotasFiscais.Add(nota);
            if (numeracao is null) { numeracao = new NumeracaoNfe { EmpresaId = tenant.EmpresaId, Ambiente = config.Ambiente, Serie = request.Serie }; db.NumeracoesNfe.Add(numeracao); }
            numeracao.ProximoNumero = checked(numero + 1);
            // Reserva numero, chave, XML assinado e itens antes de qualquer chamada externa.
            await db.SaveChangesAsync(token);
            await using var noteGate = await FiscalLock.AdquirirAsync(db, -nota.Id, CancellationToken.None);
            if (noteGate is null) return Ok(ToResponse(nota));
            await Transmitir(nota, cert, token);
            return Ok(ToResponse(nota));
        }
        catch (Exception e) when (e is ArgumentException or CryptographicException or XmlSchemaException)
        { return BadRequest(e is CryptographicException ? "Certificado A1 invalido ou senha incorreta." : e.Message); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return Conflict("A solicitacao ou numeracao ja foi registrada. Consulte as notas antes de repetir a operacao."); }
    }

    [HttpPost("{id:long}/autorizacao/consultar")]
    public async Task<IActionResult> Consultar(long id, CertificadoRequest request, CancellationToken token)
    {
        await using var gate = await FiscalLock.AdquirirAsync(db, -id, token);
        if (gate is null) return Conflict("Existe uma operacao em andamento nesta nota.");
        var nota = await db.NotasFiscais.SingleOrDefaultAsync(n => n.Id == id && n.EmpresaId == tenant.EmpresaId, token);
        if (nota is null) return NotFound();
        if (nota.XmlEnvioNfe.Length == 0) return BadRequest("Utilize a consulta da nota importada.");
        if (nota.Situacao is "Autorizada" or "Cancelada" or "Rejeitada") return Ok(ToResponse(nota));
        try
        {
            using var cert = SefazEventosService.CarregarCertificado(request.Base64, request.Password);
            NfeAssinatura.ValidarTitular(cert, nota.ChaveAcesso.Substring(6, 14));
            var retorno = nota.Recibo.Length > 0 ? await sefaz.ConsultarReciboAsync(nota, cert, token) : new System.Xml.Linq.XDocument(await eventos.ConsultarAsync(nota, cert, token));
            NfeAutorizacaoService.AplicarRetorno(nota, retorno);
            await new NfeEstoqueSaidaService(db).SincronizarAsync(nota, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            return Ok(ToResponse(nota));
        }
        catch (Exception e) when (e is ArgumentException or CryptographicException) { return BadRequest("Nao foi possivel validar o certificado: " + e.Message); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or XmlException)
        { return Problem(statusCode: 502, detail: "Consulta sem confirmacao. A nota mantem a mesma chave e deve ser consultada novamente."); }
    }

    [HttpPost("{id:long}/autorizacao/reenviar")]
    public async Task<IActionResult> Reenviar(long id, CertificadoRequest request, CancellationToken token)
    {
        await using var gate = await FiscalLock.AdquirirAsync(db, -id, token);
        if (gate is null) return Conflict("Existe uma operacao em andamento nesta nota.");
        var nota = await db.NotasFiscais.SingleOrDefaultAsync(n => n.Id == id && n.EmpresaId == tenant.EmpresaId, token);
        if (nota is null) return NotFound();
        if (nota.Situacao != "Processando" || nota.CodigoAutorizacao != "217" || nota.Recibo.Length > 0 || nota.XmlEnvioNfe.Length == 0)
            return Conflict("Consulte a SEFAZ antes de reenviar. Somente notas nao localizadas podem reutilizar o mesmo XML.");
        try
        {
            using var cert = SefazEventosService.CarregarCertificado(request.Base64, request.Password);
            NfeAssinatura.ValidarTitular(cert, nota.ChaveAcesso.Substring(6, 14));
            nota.CodigoAutorizacao = ""; nota.MotivoAutorizacao = "Reenvio do mesmo XML assinado em andamento.";
            await db.SaveChangesAsync(token);
            await Transmitir(nota, cert, token);
            return Ok(ToResponse(nota));
        }
        catch (Exception e) when (e is ArgumentException or CryptographicException) { return BadRequest(e.Message); }
    }

    private async Task Transmitir(NotaFiscal nota, System.Security.Cryptography.X509Certificates.X509Certificate2 cert, CancellationToken token)
    {
        try
        {
            var result = await sefaz.AutorizarAsync(nota, cert, token);
            NfeAutorizacaoService.AplicarRetorno(nota, result);
            await new NfeEstoqueSaidaService(db).SincronizarAsync(nota, CancellationToken.None);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or XmlException)
        {
            nota.MotivoAutorizacao = "Sem confirmacao da SEFAZ. Consulte a mesma nota antes de qualquer novo envio.";
        }
        await db.SaveChangesAsync(CancellationToken.None);
    }
    public static EmissaoNfeResponse ToResponse(NotaFiscal n) => new(n.Id, n.Numero, n.Serie, n.ChaveAcesso, n.Ambiente, n.Situacao, n.CodigoAutorizacao, n.MotivoAutorizacao, n.Protocolo, n.Situacao == "Processando" && n.CodigoAutorizacao == "217" && n.Recibo.Length == 0);
}
