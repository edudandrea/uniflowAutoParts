using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using back.Models;

namespace back.Services;

public record SefazEventoResultado(string Codigo, string Motivo, string Protocolo, string Xml)
{
    public bool Confirmado => Codigo == "135" && Protocolo.Length == 15 && Protocolo.All(char.IsAsciiDigit);
}

public class SefazEventosService
{
    public const string CondicoesCorrecao = "A Carta de Correcao e disciplinada pelo paragrafo 1o-A do art. 7o do Convenio S/N, de 15 de dezembro de 1970 e pode ser utilizada para regularizacao de erro ocorrido na emissao de documento fiscal, desde que o erro nao esteja relacionado com: I - as variaveis que determinam o valor do imposto tais como: base de calculo, aliquota, diferenca de preco, quantidade, valor da operacao ou da prestacao; II - a correcao de dados cadastrais que implique mudanca do remetente ou do destinatario; III - a data de emissao ou de saida.";
    private const string ServiceNamespace = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4";

    public static string? Bloqueio(NotaFiscal nota) =>
        nota.Ambiente != 2 ? "A integracao atual atende somente homologacao. Notas de producao estao disponiveis para consulta e download." :
        !nota.ChaveAcesso.StartsWith("43", StringComparison.Ordinal) ? "A integracao direta atual atende somente a SEFAZ/RS." :
        nota.ChaveAcesso.Length != 44 || nota.ChaveAcesso[34] != '1' ? "Eventos de notas emitidas em contingencia ainda nao estao habilitados." :
        nota.Situacao != "Autorizada" ? "Somente notas autorizadas podem receber estes eventos." : null;

    public static X509Certificate2 CarregarCertificado(string base64, string password)
    {
        if (string.IsNullOrWhiteSpace(base64) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Cadastre o certificado A1 e a senha em Configuracoes > Certificado e Tributacoes.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { throw new ArgumentException("Certificado A1 invalido."); }
        try
        {
            if (bytes.Length is 0 or > 1_048_576) throw new ArgumentException("O certificado deve ter ate 1 MB.");
            var cert = X509CertificateLoader.LoadPkcs12(bytes, password, X509KeyStorageFlags.EphemeralKeySet);
            if (!cert.HasPrivateKey || cert.NotBefore.ToUniversalTime() > DateTime.UtcNow || cert.NotAfter.ToUniversalTime() < DateTime.UtcNow)
            {
                cert.Dispose();
                throw new ArgumentException("Certificado vencido, fora da validade ou sem chave privada.");
            }
            return cert;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static string CriarEvento(NotaFiscal nota, string tipo, int sequencia, string texto, X509Certificate2 cert)
    {
        var bloqueio = Bloqueio(nota);
        if (bloqueio is not null) throw new ArgumentException(bloqueio);
        if (tipo is not ("110110" or "110111")) throw new ArgumentException("Tipo de evento invalido.");
        if (tipo == "110110" && nota.Tipo != "NFe") throw new ArgumentException("Carta de correcao disponivel somente para NF-e.");
        if (sequencia < 1 || sequencia > (tipo == "110111" ? 1 : 20)) throw new ArgumentException("Limite de eventos atingido.");
        texto = texto.Trim();
        if (texto.Length < 15 || texto.Length > (tipo == "110111" ? 255 : 1000))
            throw new ArgumentException(tipo == "110111" ? "A justificativa deve ter de 15 a 255 caracteres." : "A correcao deve ter de 15 a 1000 caracteres.");
        XNamespace ns = NotaFiscalXml.Ns;
        var id = $"ID{tipo}{nota.ChaveAcesso}{sequencia:00}";
        var det = new XElement(ns + "detEvento", new XAttribute("versao", "1.00"),
            new XElement(ns + "descEvento", tipo == "110111" ? "Cancelamento" : "Carta de Correcao"));
        if (tipo == "110111") det.Add(new XElement(ns + "nProt", nota.Protocolo), new XElement(ns + "xJust", texto));
        else det.Add(new XElement(ns + "xCorrecao", texto), new XElement(ns + "xCondUso", CondicoesCorrecao));
        var evento = new XElement(ns + "evento", new XAttribute("versao", "1.00"),
            new XElement(ns + "infEvento", new XAttribute("Id", id),
                new XElement(ns + "cOrgao", "43"), new XElement(ns + "tpAmb", "2"),
                new XElement(ns + "CNPJ", nota.ChaveAcesso.Substring(6, 14)),
                new XElement(ns + "chNFe", nota.ChaveAcesso),
                new XElement(ns + "dhEvento", DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)),
                new XElement(ns + "tpEvento", tipo), new XElement(ns + "nSeqEvento", sequencia),
                new XElement(ns + "verEvento", "1.00"), det));
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        doc.LoadXml(evento.ToString(SaveOptions.DisableFormatting));
        using var rsa = cert.GetRSAPrivateKey() ?? throw new ArgumentException("O certificado deve possuir chave RSA.");
        var signed = new SignedXml(doc) { SigningKey = rsa };
        signed.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signed.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
        var reference = new Reference("#" + id) { DigestMethod = SignedXml.XmlDsigSHA1Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        signed.AddReference(reference);
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signed.KeyInfo = keyInfo;
        signed.ComputeSignature();
        doc.DocumentElement!.AppendChild(doc.ImportNode(signed.GetXml(), true));
        return new XElement(ns + "envEvento", new XAttribute("versao", "1.00"),
            new XElement(ns + "idLote", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            XElement.Parse(doc.OuterXml, LoadOptions.PreserveWhitespace)).ToString(SaveOptions.DisableFormatting);
    }

    public static SefazEventoResultado LerRetorno(string xml, NotaFiscal nota, string tipo, int sequencia)
    {
        var doc = NotaFiscalXml.Ler(xml);
        var ns = NotaFiscalXml.Ns;
        var lote = doc.Descendants(ns + "retEnvEvento").SingleOrDefault();
        var info = lote?.Elements(ns + "retEvento").SingleOrDefault()?.Element(ns + "infEvento");
        var codigoLote = lote?.Element(ns + "cStat")?.Value;
        if (info is null && int.TryParse(codigoLote, out var rejeicao) && rejeicao >= 200)
            return new(codigoLote!, lote?.Element(ns + "xMotivo")?.Value ?? "Lote rejeitado pela SEFAZ.", "", xml);
        if (info is null)
            throw new XmlException("A SEFAZ nao retornou o protocolo do evento. Consulte a situacao antes de reenviar.");
        var codigo = info.Element(ns + "cStat")?.Value ?? "";
        if (codigo.Length != 3 || !codigo.All(char.IsAsciiDigit)) throw new XmlException("Retorno sem codigo de processamento valido.");
        if (info.Element(ns + "tpAmb")?.Value != nota.Ambiente.ToString(CultureInfo.InvariantCulture)
            || info.Element(ns + "chNFe")?.Value != nota.ChaveAcesso
            || info.Element(ns + "tpEvento")?.Value != tipo
            || info.Element(ns + "nSeqEvento")?.Value != sequencia.ToString(CultureInfo.InvariantCulture))
            throw new XmlException("O retorno da SEFAZ nao corresponde ao evento enviado.");
        return new(codigo, info.Element(ns + "xMotivo")?.Value ?? "Retorno sem motivo.",
            info.Element(ns + "nProt")?.Value ?? "", xml);
    }

    public virtual async Task<SefazEventoResultado> EnviarAsync(NotaFiscal nota, NotaFiscalEvento evento, X509Certificate2 cert, CancellationToken token)
    {
        using var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(cert);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var host = nota.Tipo == "NFCe" ? "nfce-homologacao" : "nfe-homologacao";
        var envelope = $"<soap12:Envelope xmlns:soap12=\"http://www.w3.org/2003/05/soap-envelope\"><soap12:Body><nfeDadosMsg xmlns=\"{ServiceNamespace}\">{evento.XmlEnvio}</nfeDadosMsg></soap12:Body></soap12:Envelope>";
        using var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");
        content.Headers.ContentType!.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("action", $"\"{ServiceNamespace}/nfeRecepcaoEvento\""));
        using var response = await client.PostAsync($"https://{host}.sefazrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx", content, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5_000_000
        });
        var doc = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, token);
        return LerRetorno(doc.ToString(SaveOptions.DisableFormatting), nota, evento.Tipo, evento.Sequencia);
    }

    public virtual async Task<XElement> ConsultarAsync(NotaFiscal nota, X509Certificate2 cert, CancellationToken token)
    {
        const string service = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4";
        var ns = NotaFiscalXml.Ns;
        var consulta = new XElement(ns + "consSitNFe", new XAttribute("versao", "4.00"),
            new XElement(ns + "tpAmb", "2"), new XElement(ns + "xServ", "CONSULTAR"), new XElement(ns + "chNFe", nota.ChaveAcesso));
        var envelope = $"<soap12:Envelope xmlns:soap12=\"http://www.w3.org/2003/05/soap-envelope\"><soap12:Body><nfeDadosMsg xmlns=\"{service}\">{consulta.ToString(SaveOptions.DisableFormatting)}</nfeDadosMsg></soap12:Body></soap12:Envelope>";
        using var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(cert);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");
        content.Headers.ContentType!.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("action", $"\"{service}/nfeConsultaNF\""));
        var host = nota.Tipo == "NFCe" ? "nfce-homologacao" : "nfe-homologacao";
        using var response = await client.PostAsync($"https://{host}.sefazrs.rs.gov.br/ws/NfeConsulta/NfeConsulta4.asmx", content, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5_000_000
        });
        var doc = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, token);
        var retorno = doc.Descendants(ns + "retConsSitNFe").SingleOrDefault();
        if (retorno?.Element(ns + "chNFe")?.Value != nota.ChaveAcesso || retorno.Element(ns + "tpAmb")?.Value != "2")
            throw new XmlException("Resposta da consulta nao corresponde a nota fiscal.");
        return retorno;
    }
}
