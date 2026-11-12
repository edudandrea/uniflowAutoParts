using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace back.Services;

public static class NfeAssinatura
{
    public static void ValidarTitular(X509Certificate2 cert, string cnpj)
    {
        var documentos = Regex.Matches(cert.Subject, @"\b\d{14}\b").Select(m => m.Value).ToList();
        foreach (var extension in cert.Extensions.Where(e => e.Oid?.Value == "2.5.29.17"))
        {
            try
            {
                var names = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
                while (names.HasData)
                {
                    if (!names.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 0))) { names.ReadEncodedValue(); continue; }
                    var other = names.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0));
                    var oid = other.ReadObjectIdentifier();
                    if (oid != "2.16.76.1.3.3") continue;
                    var value = other.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0));
                    var tag = (UniversalTagNumber)value.PeekTag().TagValue;
                    if (tag is UniversalTagNumber.UTF8String or UniversalTagNumber.PrintableString or UniversalTagNumber.IA5String)
                        documentos.Add(value.ReadCharacterString(tag));
                }
            }
            catch (AsnContentException) { }
        }
        if (cnpj.Length != 14 || !documentos.Any(d => d.Length == 14 && d[..8] == cnpj[..8]))
            throw new ArgumentException("O certificado A1 deve pertencer ao mesmo CNPJ-base do emitente.");
    }

    public static string Assinar(XElement root, string elemento, X509Certificate2 cert)
    {
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        doc.LoadXml(root.ToString(SaveOptions.DisableFormatting));
        var info = doc.GetElementsByTagName(elemento, NotaFiscalXml.Ns.NamespaceName).OfType<XmlElement>().Single();
        var id = info.GetAttribute("Id");
        using var rsa = cert.GetRSAPrivateKey() ?? throw new ArgumentException("O certificado deve possuir chave RSA.");
        var signed = new SignedXml(doc) { SigningKey = rsa };
        signed.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signed.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
        var reference = new Reference("#" + id) { DigestMethod = SignedXml.XmlDsigSHA1Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform()); reference.AddTransform(new XmlDsigC14NTransform());
        signed.AddReference(reference);
        signed.KeyInfo = new KeyInfo(); signed.KeyInfo.AddClause(new KeyInfoX509Data(cert));
        signed.ComputeSignature(); doc.DocumentElement!.AppendChild(doc.ImportNode(signed.GetXml(), true));
        return doc.OuterXml;
    }
}