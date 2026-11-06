using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using back.Models;

namespace back.Services;

public static class NotaFiscalXml
{
    public static readonly XNamespace Ns = "http://www.portalfiscal.inf.br/nfe";

    public static XDocument Ler(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5_000_000
        });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    public static NotaFiscal Importar(string xml, int empresaId, string cnpj)
    {
        var doc = Ler(xml);
        var root = doc.Root;
        if (root?.Name != Ns + "nfeProc") throw new ArgumentException("Importe o XML processado (nfeProc) com protocolo de autorizacao.");
        var info = root.Element(Ns + "NFe")?.Element(Ns + "infNFe")
            ?? throw new ArgumentException("XML sem os dados da nota fiscal.");
        var ide = info.Element(Ns + "ide")!;
        var protocolo = root.Element(Ns + "protNFe")?.Element(Ns + "infProt")
            ?? throw new ArgumentException("XML sem protocolo de autorizacao.");
        var chave = protocolo.Element(Ns + "chNFe")?.Value ?? "";
        string Read(XElement? node, string name) => node?.Element(Ns + name)?.Value ?? "";
        var emitente = Read(info.Element(Ns + "emit"), "CNPJ");
        var cnpjEmpresa = new string(cnpj.Where(char.IsAsciiDigit).ToArray());
        if (chave.Length != 44 || !chave.All(char.IsAsciiDigit) || info.Attribute("Id")?.Value != "NFe" + chave)
            throw new ArgumentException("Chave de acesso invalida ou divergente no XML.");
        if (cnpjEmpresa.Length != 14 || emitente != cnpjEmpresa || chave.Substring(6, 14) != emitente)
            throw new ArgumentException("O emitente do XML deve ser a empresa selecionada. Notas de compra devem ser importadas em Compras.");
        var modelo = Read(ide, "mod");
        if (modelo is not ("55" or "65") || chave.Substring(20, 2) != modelo || Read(ide, "tpNF") != "1")
            throw new ArgumentException("Selecione uma NF-e ou NFC-e de saida.");
        var ambiente = Read(ide, "tpAmb");
        if (ambiente is not ("1" or "2") || Read(protocolo, "tpAmb") != ambiente)
            throw new ArgumentException("Ambiente fiscal invalido ou divergente.");
        if (Read(protocolo, "cStat") is not ("100" or "150") || Read(protocolo, "nProt").Length != 15)
            throw new ArgumentException("O XML nao possui protocolo de autorizacao valido.");
        if (!DateTimeOffset.TryParse(Read(ide, "dhEmi"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var emissao)
            || !decimal.TryParse(info.Element(Ns + "total")?.Element(Ns + "ICMSTot")?.Element(Ns + "vNF")?.Value,
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor) || valor < 0)
            throw new ArgumentException("Data de emissao ou valor da nota invalido.");
        var dest = info.Element(Ns + "dest");
        return new NotaFiscal
        {
            EmpresaId = empresaId, Numero = Read(ide, "nNF"), Serie = Read(ide, "serie"), Emissao = emissao.UtcDateTime,
            Cliente = Read(dest, "xNome"), DocumentoCliente = Read(dest, "CNPJ") is { Length: > 0 } cpfCnpj ? cpfCnpj : Read(dest, "CPF"),
            Tipo = modelo == "55" ? "NFe" : "NFCe", Valor = valor, ChaveAcesso = chave,
            Protocolo = Read(protocolo, "nProt"), Ambiente = int.Parse(ambiente), Xml = xml
        };
    }
}
