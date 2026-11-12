using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back.Data;
using back.Services;

namespace back.Controllers;

[ApiController]
[Route("api/fiscal")]
public class FiscalController(AppDbContext db, TenantContext tenant) : ControllerBase
{

    [HttpPost("sefaz/status")]
    [RequestSizeLimit(1_500_000)]
    public async Task<IActionResult> ConsultarStatus(CertificadoRequest request, CancellationToken cancellationToken)
    {
        if (tenant.EmpresaId <= 0) return Unauthorized();
        var ambiente = await db.ConfiguracoesFiscais.AsNoTracking().Where(c => c.EmpresaId == tenant.EmpresaId)
            .Select(c => (int?)c.Ambiente).SingleOrDefaultAsync(cancellationToken) ?? 2;
        var validation = ValidarCertificado(request);
        if (validation is not OkObjectResult) return validation;
        var bytes = Convert.FromBase64String(request.Base64);
        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12(bytes, request.Password,
                X509KeyStorageFlags.EphemeralKeySet);
            using var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            var envelope = SefazAmbientes.EnvelopeStatus(ambiente);
            using var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");
            content.Headers.ContentType!.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue(
                "action", "\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4/nfeStatusServicoNF\""));
            using var response = await client.PostAsync(SefazAmbientes.UrlStatus(ambiente), content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Problem(statusCode: 502, detail: "A SEFAZ/RS não aceitou a conexão. Verifique o certificado e o credenciamento do emitente.");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000
            });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            XNamespace ns = "http://www.portalfiscal.inf.br/nfe";
            var result = document.Descendants(ns + "retConsStatServ").SingleOrDefault();
            var code = result?.Element(ns + "cStat")?.Value;
            var reason = result?.Element(ns + "xMotivo")?.Value;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(reason) || result?.Element(ns + "tpAmb")?.Value != ambiente.ToString(System.Globalization.CultureInfo.InvariantCulture))
                return Problem(statusCode: 502, detail: "A SEFAZ retornou uma resposta de status inesperada.");
            return Ok(new { codigo = code, motivo = reason, ambiente = SefazAmbientes.Nome(ambiente), uf = "RS" });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or XmlException or CryptographicException)
        {
            return Problem(statusCode: 502, detail: "Não foi possível consultar a SEFAZ/RS. Verifique o certificado, a conexão e tente novamente.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    [HttpPost("certificado/validar")]
    [RequestSizeLimit(1_500_000)]
    public IActionResult ValidarCertificado(CertificadoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Base64) || string.IsNullOrEmpty(request.Password))
            return Problem(statusCode: 400, detail: "Informe o certificado A1 e a senha.");
        try
        {
            var bytes = Convert.FromBase64String(request.Base64);
            if (bytes.Length == 0 || bytes.Length > 1_048_576)
                return Problem(statusCode: 400, detail: "O certificado deve ter até 1 MB.");
            try
            {
                using var certificate = X509CertificateLoader.LoadPkcs12(bytes, request.Password,
                    X509KeyStorageFlags.EphemeralKeySet);
                if (!certificate.HasPrivateKey)
                    return Problem(statusCode: 400, detail: "O arquivo não contém a chave privada do certificado.");
                if (DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() || DateTime.UtcNow > certificate.NotAfter.ToUniversalTime())
                    return Problem(statusCode: 400, detail: "Certificado vencido ou ainda fora do período de validade.");
                return Ok(new { validade = certificate.NotAfter.ToUniversalTime() });
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return Problem(statusCode: 400, detail: "Arquivo de certificado inválido ou senha incorreta.");
        }
    }
}

public record CertificadoRequest(string Base64, string Password);
