using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace back.Services;

public class SefazTransporte
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
        using var response = await client.PostAsync(SefazAmbientes.UrlServico(servico, ambiente, tipo), content, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5_000_000 });
        return await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, token);
    }
}