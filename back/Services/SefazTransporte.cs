using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace back.Services;

public class SefazTransporte(ILogger<SefazTransporte>? logger = null)
{
    public virtual async Task<XDocument> EnviarAsync(string servico, int ambiente, string tipo, string xml,
        X509Certificate2 cert, CancellationToken token)
    {
        var serviceNamespace = "http://www.portalfiscal.inf.br/nfe/wsdl/" + servico;
        var action = servico switch
        {
            "NFeAutorizacao4" => "nfeAutorizacaoLote",
            "NFeRetAutorizacao4" => "nfeRetAutorizacaoLote",
            "NFeConsultaProtocolo4" => "nfeConsultaNF",
            "NFeRecepcaoEvento4" => "nfeRecepcaoEvento",
            _ => throw new ArgumentException("Servico fiscal invalido.")
        };
        using var handler = new HttpClientHandler { AllowAutoRedirect = false }; handler.ClientCertificates.Add(cert);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        var envelope = $"<soap12:Envelope xmlns:soap12=\"http://www.w3.org/2003/05/soap-envelope\"><soap12:Body><nfeDadosMsg xmlns=\"{serviceNamespace}\">{xml}</nfeDadosMsg></soap12:Body></soap12:Envelope>";
        using var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");
        content.Headers.ContentType!.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("action", $"\"{serviceNamespace}/{action}\""));
        var url = SefazAmbientes.UrlServico(servico, ambiente, tipo);
        var clock = Stopwatch.StartNew();
        int? httpStatus = null;
        logger?.LogInformation("SEFAZ: iniciando {Servico}, ambiente {Ambiente}, modelo {Modelo}, destino {Destino}.", servico, ambiente, tipo, url);
        try
        {
            using var response = await client.PostAsync(url, content, token);
            httpStatus = (int)response.StatusCode;
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5_000_000 });
            var document = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, token);
            var codes = string.Join(",", document.Descendants(NotaFiscalXml.Ns + "cStat").Select(e => e.Value).Take(5));
            logger?.LogInformation("SEFAZ: resposta de {Servico}, ambiente {Ambiente}, HTTP {HttpStatus}, cStat {Codigos}, duracao {DuracaoMs} ms.", servico, ambiente, httpStatus, codes, clock.ElapsedMilliseconds);
            return document;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or XmlException)
        {
            // Registra o diagnostico tecnico sem XML fiscal, certificado, senha ou corpo da resposta.
            logger?.LogWarning(error, "SEFAZ: falha em {Servico}, ambiente {Ambiente}, destino {Destino}, HTTP {HttpStatus}, duracao {DuracaoMs} ms, requisicao cancelada {Cancelada}, diagnostico {Diagnostico}.",
                servico, ambiente, url, httpStatus, clock.ElapsedMilliseconds, token.IsCancellationRequested, ResumirErro(error));
            throw;
        }
    }
    // Mantem as causas na mesma linha mesmo quando a hospedagem separa o stack trace.
    private static string ResumirErro(Exception error)
    {
        var causes = new List<string>();
        for (Exception? cause = error; cause is not null && causes.Count < 6; cause = cause.InnerException)
        {
            var message = new string(cause.Message.Select(c => char.IsControl(c) ? ' ' : c).Take(400).ToArray());
            var code = cause switch
            {
                HttpRequestException http => $" [{http.HttpRequestError}]",
                System.Net.Sockets.SocketException socket => $" [{socket.SocketErrorCode}]",
                _ => ""
            };
            causes.Add($"{cause.GetType().FullName}{code}: {message}");
        }
        return string.Join(" -> ", causes);
    }
}
