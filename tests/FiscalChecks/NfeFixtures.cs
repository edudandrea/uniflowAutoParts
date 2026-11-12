using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using back.Contracts;
using back.Models;
using back.Services;

public static class NfeFixtures
{
    public const string Cnpj = "12345678000195";
    public static X509Certificate2 Certificate(string cnpj = Cnpj)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Fiscal fixture:" + cnpj, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
    }
    public static Empresas Empresa(int id = 1) => new() { Id = id, Cnpj = Cnpj, RazaoSocial = "Empresa de teste fiscal", InscricaoEstadual = "2243658790", Endereco = "Rua de teste", Numero = "100", Bairro = "Centro", Cidade = "Porto Alegre", Estado = "RS", Cep = "90000000" };
    public static ConfiguracaoFiscal Config(int id = 1) => new() { EmpresaId = id, Ambiente = 1, CodigoMunicipio = "4314902", SeriePadrao = 1 };
    public static ConfiguracaoTributaria Tax(int id = 1) => new() { EmpresaId = id, Regime = "1", IcmsCode = "101", IcmsRate = 2, PisCode = "49", CofinsCode = "49", PisRate = 0, CofinsRate = 0 };
    public static CadastroItens Produto(int id = 1) => new() { Id = id, EmpresaId = 1, SKU = id, Descricao = "Filtro de oleo", NCM = 84212300, CEST = 100200, OrigemMercadoria = "0", UnidadeComercial = "UN", Custo = 10, Preco = 20, Ativo = true };
    public static Cliente Cliente(long id = 1) => new() { Id = id, TenantId = 1, NomeRazaoSocial = "Cliente de teste", CpfCnpj = "98765432000199", Ativo = true, ConsumidorFinal = false, IndicadorIe = IndicadorIe.Contribuinte, InscricaoEstadual = "2243658790", CadastroEndereco = new CadastroEndereco { EmpresaId = 1, Nome = "Endereco do cliente", Logradouro = "Rua do cliente", Numero = "20", Bairro = "Centro", Municipio = "Porto Alegre", CodigoIbgeMunicipio = "4314902", Uf = "RS", Cep = "90000000" } };
    public static EmitirNfeRequest Request(Guid? id = null) => new(id ?? Guid.NewGuid(), 1, 1, 1, "Venda de mercadoria", "5102", "55", 0, 0, 0, "Teste local sem transmissao", [new(1, 2, 20, 0)], [new("Pix", 40)], "", "");
    public static XElement Protocolo(XElement nfe, string code = "100", int ambiente = 1)
    {
        var ns = NotaFiscalXml.Ns; XNamespace ds = System.Security.Cryptography.Xml.SignedXml.XmlDsigNamespaceUrl;
        return new XElement(ns + "protNFe", new XAttribute("versao", "4.00"), new XElement(ns + "infProt",
            new XElement(ns + "tpAmb", ambiente), new XElement(ns + "verAplic", "fixture-1"),
            new XElement(ns + "chNFe", nfe.Element(ns + "infNFe")!.Attribute("Id")!.Value[3..]),
            new XElement(ns + "dhRecbto", DateTimeOffset.UtcNow.ToString("O")), new XElement(ns + "nProt", "143260000000001"),
            new XElement(ns + "digVal", nfe.Descendants(ds + "DigestValue").Single().Value), new XElement(ns + "cStat", code), new XElement(ns + "xMotivo", "Retorno de teste local")));
    }
    public static XDocument Autorizacao(XElement nfe, string code = "100", int ambiente = 1) => new(new XElement(NotaFiscalXml.Ns + "retEnviNFe", new XAttribute("versao", "4.00"), new XElement(NotaFiscalXml.Ns + "tpAmb", ambiente), new XElement(NotaFiscalXml.Ns + "cStat", "104"), new XElement(NotaFiscalXml.Ns + "xMotivo", "Lote processado"), Protocolo(nfe, code, ambiente)));
}