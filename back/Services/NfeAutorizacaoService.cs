using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using back.Models;

namespace back.Services;

public class NfeAutorizacaoService(SefazTransporte transporte)
{
    public virtual Task<XDocument> AutorizarAsync(NotaFiscal nota, X509Certificate2 cert, CancellationToken token)
    {
        var ns = NotaFiscalXml.Ns;
        var lote = new XElement(ns + "enviNFe", new XAttribute("versao", "4.00"),
            new XElement(ns + "idLote", nota.Id), new XElement(ns + "indSinc", "1"), XElement.Parse(nota.XmlEnvioNfe, LoadOptions.PreserveWhitespace));
        return transporte.EnviarAsync("NFeAutorizacao4", nota.Ambiente, nota.Tipo, lote.ToString(SaveOptions.DisableFormatting), cert, token);
    }
    public virtual Task<XDocument> ConsultarReciboAsync(NotaFiscal nota, X509Certificate2 cert, CancellationToken token)
    {
        var ns = NotaFiscalXml.Ns;
        var xml = new XElement(ns + "consReciNFe", new XAttribute("versao", "4.00"), new XElement(ns + "tpAmb", nota.Ambiente), new XElement(ns + "nRec", nota.Recibo));
        return transporte.EnviarAsync("NFeRetAutorizacao4", nota.Ambiente, nota.Tipo, xml.ToString(SaveOptions.DisableFormatting), cert, token);
    }

    public static void AplicarRetorno(NotaFiscal nota, XDocument doc)
    {
        var ns = NotaFiscalXml.Ns;
        var root = doc.Descendants().Prepend(doc.Root!).FirstOrDefault(e => e?.Name == ns + "retEnviNFe" || e?.Name == ns + "retConsReciNFe" || e?.Name == ns + "retConsSitNFe")
            ?? throw new XmlException("Resposta sem resultado de autorizacao.");
        if (root.Element(ns + "tpAmb")?.Value != nota.Ambiente.ToString(CultureInfo.InvariantCulture))
            throw new XmlException("Retorno pertence a outro ambiente fiscal.");
        if (root.Name.LocalName == "retConsSitNFe" && root.Element(ns + "chNFe")?.Value != nota.ChaveAcesso)
            throw new XmlException("Consulta retornou outra chave de acesso.");
        var protocolo = root.Element(ns + "protNFe");
        var info = protocolo?.Element(ns + "infProt");
        var codigoConsulta = root.Element(ns + "cStat")?.Value;
        var codigo = root.Name.LocalName == "retConsSitNFe" && codigoConsulta is "101" or "151" or "155" ? codigoConsulta : info?.Element(ns + "cStat")?.Value ?? codigoConsulta ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(codigo, @"^\d{3}$")) throw new XmlException("Retorno sem codigo fiscal valido.");
        if (info is not null && (info.Element(ns + "chNFe")?.Value != nota.ChaveAcesso || info.Element(ns + "tpAmb")?.Value != nota.Ambiente.ToString()))
            throw new XmlException("O protocolo retornado nao corresponde a nota enviada.");
        if ((nota.Situacao == "Cancelada" && (codigo is "100" or "150")) || ((nota.Situacao is "Autorizada" or "Cancelada") && (codigo is "103" or "104" or "105" or "106" or "108" or "109" or "204" or "217" or "539"))) return;
        if (codigo is "100" or "150")
        {
            if (info is null || !System.Text.RegularExpressions.Regex.IsMatch(info.Element(ns + "nProt")?.Value ?? "", @"^\d{15}$"))
                throw new XmlException("A autorizacao nao possui protocolo valido.");
            var signed = NotaFiscalXml.Ler(nota.XmlEnvioNfe);
            XNamespace ds = System.Security.Cryptography.Xml.SignedXml.XmlDsigNamespaceUrl;
            if (info.Element(ns + "digVal")?.Value != signed.Descendants(ds + "DigestValue").Single().Value)
                throw new XmlException("O protocolo nao corresponde ao XML assinado enviado.");
            if (!DateTimeOffset.TryParse(info.Element(ns + "dhRecbto")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var autorizadoEm))
                throw new XmlException("Protocolo sem data de autorizacao valida.");
            nota.Protocolo = info.Element(ns + "nProt")!.Value;
            nota.AutorizadaEm = autorizadoEm.UtcDateTime;
            nota.Xml = new XElement(ns + "nfeProc", new XAttribute("versao", "4.00"),
                XElement.Parse(nota.XmlEnvioNfe, LoadOptions.PreserveWhitespace), new XElement(protocolo!)).ToString(SaveOptions.DisableFormatting);
            nota.Situacao = "Autorizada";
        }
        else if (codigo is "101" or "151" or "155") nota.Situacao = "Cancelada";
        else if (codigo is "103" or "104" or "105" or "106" or "108" or "109" or "204" or "217" or "539")
        {
            nota.Situacao = "Processando";
            var recibo = root.Element(ns + "infRec")?.Element(ns + "nRec")?.Value;
            if (recibo is not null) nota.Recibo = recibo;
            if (codigo == "104" && info is null) throw new XmlException("Lote processado sem protocolo. Consulte a SEFAZ antes de reenviar.");
        }
        else if (int.Parse(codigo) >= 200) nota.Situacao = "Rejeitada";
        else throw new XmlException("Resposta de autorizacao nao conclusiva.");
        nota.CodigoAutorizacao = codigo;
        nota.MotivoAutorizacao = info?.Element(ns + "xMotivo")?.Value ?? root.Element(ns + "xMotivo")?.Value ?? "Retorno da SEFAZ.";
        nota.XmlRetornoAutorizacao = doc.ToString(SaveOptions.DisableFormatting);
    }
}