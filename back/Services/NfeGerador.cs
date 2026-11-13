using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using back.Contracts;
using back.Models;

namespace back.Services;

public static class NfeGerador
{
    private static readonly XNamespace Ns = NotaFiscalXml.Ns;
    public static string Digitos(string? text) => new((text ?? "").Where(char.IsAsciiDigit).ToArray());
    public static decimal Moeda(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static XElement E(string name, object? value) => new(Ns + name, value);
    private static string F(decimal value, int decimals = 2) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    private static void Exigir(bool condition, string error) { if (!condition) throw new ArgumentException(error); }
    private static bool Texto(string? value, int min, int max) => value is not null && value.Trim().Length >= min && value.Trim().Length <= max;

    public static void ValidarEmitente(Empresas empresa, ConfiguracaoFiscal config)
    {
        Exigir(Digitos(empresa.Cnpj).Length == 14, "Cadastre o CNPJ do emitente.");
        Exigir(Texto(empresa.RazaoSocial, 2, 60) && Regex.IsMatch(Digitos(empresa.InscricaoEstadual), @"^\d{2,14}$"), "Cadastre a razao social e a inscricao estadual do emitente.");
        Exigir(empresa.Estado?.ToUpperInvariant() == "RS", "A integracao direta de emissao atende emitentes do RS.");
        ValidarEndereco(empresa.Endereco, empresa.Numero, empresa.Bairro, empresa.Cidade, config.CodigoMunicipio, empresa.Estado, empresa.Cep, "emitente");
    }

    private static void ValidarEndereco(string? rua, string? numero, string? bairro, string? cidade, string? ibge, string? uf, string? cep, string tipo)
    {
        Exigir(Texto(rua, 2, 60) && Texto(numero, 1, 60) && Texto(bairro, 2, 60) && Texto(cidade, 2, 60), $"Complete logradouro, numero, bairro e municipio do {tipo}.");
        Exigir(Regex.IsMatch(ibge ?? "", @"^\d{7}$") && ibge != "0000000" && ibge!.StartsWith("43") && uf?.ToUpperInvariant() == "RS", $"Informe um municipio IBGE valido do RS para o {tipo}. Vendas interestaduais exigem parametrizacao fiscal propria.");
        Exigir(Digitos(cep).Length == 8, $"Informe um CEP valido do {tipo}.");
    }

    private static XElement EnderecoXml(string name, string rua, string numero, string? complemento, string bairro, string cidade, string ibge, string uf, string cep)
    {
        var endereco = E(name, new object[] { E("xLgr", rua.Trim()), E("nro", numero.Trim()) });
        if (!string.IsNullOrWhiteSpace(complemento)) endereco.Add(E("xCpl", complemento.Trim()));
        endereco.Add(E("xBairro", bairro.Trim()), E("cMun", ibge), E("xMun", cidade.Trim()), E("UF", uf.ToUpperInvariant()), E("CEP", Digitos(cep)), E("cPais", "1058"), E("xPais", "BRASIL"));
        return endereco;
    }

    public static string Chave(string cnpj, DateTimeOffset emissao, int serie, int numero, string codigo)
    {
        var baseKey = "43" + emissao.ToString("yyMM", CultureInfo.InvariantCulture) + Digitos(cnpj) + "55" + serie.ToString("D3") + numero.ToString("D9") + "1" + codigo;
        Exigir(baseKey.Length == 43 && baseKey.All(char.IsAsciiDigit), "Dados invalidos para a chave da NF-e.");
        var sum = 0; var weight = 2;
        for (var i = baseKey.Length - 1; i >= 0; i--) { sum += (baseKey[i] - '0') * weight; weight = weight == 9 ? 2 : weight + 1; }
        var digit = 11 - sum % 11; if (digit >= 10) digit = 0;
        return baseKey + digit;
    }

    public static void ValidarSchema(string xml)
    {
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", "NFe4", "nfe_v4.00.xsd");
        schemas.Add(Ns.NamespaceName, path); schemas.Compile(); schemas.XmlResolver = null;
        var errors = new List<string>();
        NotaFiscalXml.Ler(xml).Validate(schemas, (_, e) => { if (e.Severity == XmlSeverityType.Error) errors.Add(e.Message); });
        if (errors.Count > 0) throw new ArgumentException("XML fiscal invalido: " + string.Join(" ", errors.Take(4)));
    }

    public static NotaFiscal Criar(Empresas empresa, ConfiguracaoFiscal config, ConfiguracaoTributaria tax, Cliente cliente,
        IReadOnlyDictionary<int, CadastroItens> produtos, EmitirNfeRequest request, int numero, int usuarioId, X509Certificate2 cert)
    {
        ValidarEmitente(empresa, config);
        var icmsCode = request.Csosn ?? tax.IcmsCode;
        if (request.Destinatario is { } d)
            cliente = new Cliente { Id = cliente.Id, TenantId = cliente.TenantId, Ativo = cliente.Ativo, NomeRazaoSocial = cliente.NomeRazaoSocial,
                CpfCnpj = cliente.CpfCnpj, Relacionamento = cliente.Relacionamento, IndicadorIe = (IndicadorIe)d.IndicadorIe, ConsumidorFinal = d.ConsumidorFinal, InscricaoEstadual = d.InscricaoEstadual,
                CadastroEndereco = new CadastroEndereco { Logradouro = d.Logradouro, Numero = d.Numero, Complemento = d.Complemento, Bairro = d.Bairro, Municipio = d.Municipio, CodigoIbgeMunicipio = d.CodigoMunicipio, Uf = d.Uf, Cep = d.Cep } };
        Exigir(request.Modelo == "55", "Este fluxo emite NF-e modelo 55. A NFC-e exige CSC e configuracao especifica.");
        Exigir(request.SolicitacaoId != Guid.Empty && request.Serie is >= 0 and <= 889 && numero is > 0 and <= 999999999, "Informe solicitacao, serie e numeracao validas para NF-e.");
        Exigir(config.Ambiente == request.Ambiente && config.Ambiente is 1 or 2, "O ambiente da empresa mudou. Recarregue a venda antes de emitir.");
        Exigir(Texto(request.Natureza, 1, 60) && Regex.IsMatch(request.Cfop ?? "", @"^5\d{3}$"), "Informe natureza e CFOP de venda interna (iniciado por 5).");
        Exigir(tax.Regime is "1" or "4", "A emissao automatica atual atende o Simples Nacional e MEI. Outros regimes exigem grupos tributarios adicionais, incluindo IBS/CBS.");
        Exigir(new[] { "101", "102", "103", "300", "400" }.Contains(icmsCode), "O CSOSN selecionado exige parametrizacao de ST ou tributacao adicional. Configure o codigo aplicavel a esta venda antes de emitir.");
        foreach (var rate in new[] { tax.IcmsRate, tax.PisRate, tax.CofinsRate }) Exigir(rate is >= 0 and <= 100, "Defina as aliquotas fiscais (inclusive zero) na tributacao da empresa.");
        Exigir(cliente.Ativo && cliente.TenantId == empresa.Id && cliente.Relacionamento != RelacionamentoCadastro.Fornecedor, "Selecione um cliente ativo da empresa.");
        Exigir(icmsCode != "101" || cliente.IndicadorIe == IndicadorIe.Contribuinte && !cliente.ConsumidorFinal, "CSOSN 101 exige destinatario contribuinte com direito a credito. Na revisao da nota, selecione o CSOSN aplicavel a esta venda (102 para operacao tributada sem permissao de credito) ou corrija os dados fiscais do destinatario.");
        var documento = Digitos(cliente.CpfCnpj);
        Exigir(documento.Length is 11 or 14 && Texto(cliente.NomeRazaoSocial, 2, 60), "Complete o nome e CPF/CNPJ do destinatario.");
        Exigir((int)cliente.IndicadorIe is 1 or 2 or 9, "Cadastre o indicador da inscricao estadual do destinatario.");
        if (cliente.IndicadorIe == IndicadorIe.Contribuinte) Exigir(Digitos(cliente.InscricaoEstadual).Length is >= 2 and <= 14, "Cadastre a inscricao estadual do destinatario contribuinte.");
        var cadastro = cliente.CadastroEndereco;
        var endereco = cliente.Enderecos.OrderByDescending(e => e.Principal).FirstOrDefault();
        var rua = cadastro?.Logradouro ?? endereco?.Logradouro; var n = cadastro?.Numero ?? endereco?.Numero;
        var bairro = cadastro?.Bairro ?? endereco?.Bairro; var cidade = cadastro?.Municipio ?? endereco?.Municipio;
        var ibge = cadastro?.CodigoIbgeMunicipio ?? endereco?.CodigoIbgeMunicipio;
        var uf = cadastro?.Uf ?? endereco?.Uf; var cep = cadastro?.Cep ?? endereco?.Cep;
        var complemento = cadastro?.Complemento ?? endereco?.Complemento;
        ValidarEndereco(rua, n, bairro, cidade, ibge, uf, cep, "destinatario");
        Exigir(request.Itens is { Count: > 0 and <= 990 } && request.Itens.Select(i => i.ProdutoId).Distinct().Count() == request.Itens.Count, "Informe de 1 a 990 produtos, sem repeticoes.");
        Exigir(request.Desconto >= 0 && request.Frete >= 0 && request.Despesas >= 0 && new[] { request.Desconto, request.Frete, request.Despesas }.All(v => Moeda(v) == v), "Informe descontos, frete e despesas validos em centavos.");
        Exigir(request.ModalidadeFrete is 0 or 1 or 2 or 3 or 4 or 9 && request.IndicadorPresenca is 1 or 2 or 3 or 9, "Informe modalidade de frete e presenca do comprador validas.");
        Exigir(request.ModalidadeFrete != 9 || request.Frete == 0, "Frete cobrado exige modalidade de transporte. Revise o campo de frete.");
        Exigir(new[] { request.Desconto, request.Frete, request.Despesas }.All(v => v < 10000000000000m), "Valores excedem os limites do leiaute da NF-e.");
        var brutos = new List<decimal>(); var descontos = new List<decimal>();
        foreach (var item in request.Itens)
        {
            Exigir(produtos.TryGetValue(item.ProdutoId, out var p) && p.Ativo && p.EmpresaId == empresa.Id, "Produto nao encontrado na empresa.");
            Exigir(item.Quantidade > 0 && item.Quantidade < 100000000000m && item.PrecoUnitario < 100000000000m && decimal.Round(item.Quantidade, 4) == item.Quantidade && item.PrecoUnitario > 0 && decimal.Round(item.PrecoUnitario, 10) == item.PrecoUnitario && item.DescontoPercentual is >= 0 and <= 100, "Informe quantidades, precos e descontos validos.");
            Exigir(p!.AliquotaIpi == 0, $"Produto {p.Descricao}: configure CST/enquadramento de IPI antes de emitir operacao com IPI.");
            Exigir(Regex.IsMatch(item.Origem ?? p.OrigemMercadoria, "^[0-8]$"), $"Informe a origem fiscal do produto {p.Descricao} na revisao da venda.");
            Exigir(Regex.IsMatch(item.Ncm ?? p.NCM.ToString("D8"), @"^\d{8}$") && (item.Ncm ?? p.NCM.ToString("D8")) != "00000000", $"Cadastre o NCM de oito digitos do produto {p.Descricao}.");
            Exigir(Texto(item.Unidade ?? p.UnidadeComercial, 1, 6), $"Informe a unidade comercial do produto {p.Descricao}.");
            var bruto = Moeda(item.Quantidade * item.PrecoUnitario);
            Exigir(bruto < 10000000000000m, "Valor de item excede o limite do leiaute da NF-e.");
            brutos.Add(bruto); descontos.Add(Moeda(bruto * item.DescontoPercentual / 100));
        }
        var bases = brutos.Select((value, i) => value - descontos[i]).ToArray();
        Exigir(request.Desconto <= bases.Sum(), "O desconto geral excede o valor dos itens.");
        var descontoGeral = Ratear(request.Desconto, bases); var fretes = Ratear(request.Frete, bases); var despesas = Ratear(request.Despesas, bases);
        var total = Moeda(brutos.Sum() - descontos.Sum() - request.Desconto + request.Frete + request.Despesas);
        Exigir(total > 0 && request.Pagamentos is { Count: > 0 and <= 100 } && request.Pagamentos.All(p => p.Valor > 0 && Moeda(p.Valor) == p.Valor) && request.Pagamentos.Sum(p => p.Valor) == total, "Os pagamentos devem corresponder exatamente ao total da NF-e.");
        NfeAssinatura.ValidarTitular(cert, Digitos(empresa.Cnpj));
        var emissao = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3));
        var codigo = RandomNumberGenerator.GetInt32(0, 100000000).ToString("D8");
        var chave = Chave(empresa.Cnpj!, emissao, request.Serie, numero, codigo);
        var inf = new XElement(Ns + "infNFe", new XAttribute("Id", "NFe" + chave), new XAttribute("versao", "4.00"),
            E("ide", new object[] { E("cUF", "43"), E("cNF", codigo), E("natOp", request.Natureza.Trim()), E("mod", "55"), E("serie", request.Serie), E("nNF", numero), E("dhEmi", emissao.ToString("yyyy-MM-dd'T'HH:mm:sszzz")), E("tpNF", "1"), E("idDest", "1"), E("cMunFG", config.CodigoMunicipio), E("tpImp", "1"), E("tpEmis", "1"), E("cDV", chave[^1]), E("tpAmb", config.Ambiente), E("finNFe", "1"), E("indFinal", cliente.ConsumidorFinal ? "1" : "0"), E("indPres", request.IndicadorPresenca), E("procEmi", "0"), E("verProc", "UniflowAutoParts-1.0") }),
            E("emit", new object[] { E("CNPJ", Digitos(empresa.Cnpj)), E("xNome", empresa.RazaoSocial.Trim()), EnderecoXml("enderEmit", empresa.Endereco!, empresa.Numero!, empresa.Complemento, empresa.Bairro!, empresa.Cidade!, config.CodigoMunicipio, "RS", empresa.Cep!), E("IE", Digitos(empresa.InscricaoEstadual)), E("CRT", tax.Regime) }));
        if (request.IndicadorPresenca is 2 or 3 or 9)
        {
            var ide = inf.Element(Ns + "ide")!; ide.Element(Ns + "procEmi")!.AddBeforeSelf(E("indIntermed", "0"));
        }
        var dest = E("dest", new object[] { E(documento.Length == 14 ? "CNPJ" : "CPF", documento), E("xNome", config.Ambiente == 2 ? "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL" : cliente.NomeRazaoSocial.Trim()), EnderecoXml("enderDest", rua!, n!, complemento, bairro!, cidade!, ibge!, uf!, cep!), E("indIEDest", (int)cliente.IndicadorIe) });
        if (cliente.IndicadorIe == IndicadorIe.Contribuinte) dest.Add(E("IE", Digitos(cliente.InscricaoEstadual)));
        inf.Add(dest);
        var pisTotal = 0m; var cofinsTotal = 0m;
        for (var i = 0; i < request.Itens.Count; i++)
        {
            var item = request.Itens[i]; var p = produtos[item.ProdutoId]; var desconto = descontos[i] + descontoGeral[i];
            var baseFiscal = brutos[i] - desconto + fretes[i] + despesas[i];
            var prod = E("prod", new object[] { E("cProd", p.SKU), E("cEAN", p.Ean ?? "SEM GTIN"), E("xProd", p.Descricao.Trim()), E("NCM", item.Ncm ?? p.NCM.ToString("D8")) });
            if (p.CEST > 0) prod.Add(E("CEST", p.CEST.ToString("D7")));
            prod.Add(E("CFOP", request.Cfop), E("uCom", item.Unidade ?? p.UnidadeComercial), E("qCom", F(item.Quantidade, 4)), E("vUnCom", F(item.PrecoUnitario, 10)), E("vProd", F(brutos[i])), E("cEANTrib", p.Ean ?? "SEM GTIN"), E("uTrib", item.Unidade ?? p.UnidadeComercial), E("qTrib", F(item.Quantidade, 4)), E("vUnTrib", F(item.PrecoUnitario, 10)));
            if (fretes[i] > 0) prod.Add(E("vFrete", F(fretes[i]))); if (desconto > 0) prod.Add(E("vDesc", F(desconto))); if (despesas[i] > 0) prod.Add(E("vOutro", F(despesas[i]))); prod.Add(E("indTot", "1"));
            var icms = E(icmsCode == "101" ? "ICMSSN101" : "ICMSSN102", new object[] { E("orig", item.Origem ?? p.OrigemMercadoria), E("CSOSN", icmsCode) });
            if (icmsCode == "101") icms.Add(E("pCredSN", F(tax.IcmsRate!.Value, 4)), E("vCredICMSSN", F(Moeda(baseFiscal * tax.IcmsRate.Value / 100))));
            var pis = Contribuicao("PIS", tax.PisCode, tax.PisRate!.Value, baseFiscal, out var vPis); pisTotal += vPis;
            var cofins = Contribuicao("COFINS", tax.CofinsCode, tax.CofinsRate!.Value, baseFiscal, out var vCofins); cofinsTotal += vCofins;
            inf.Add(new XElement(Ns + "det", new XAttribute("nItem", i + 1), prod, E("imposto", new object[] { E("ICMS", icms), pis, cofins })));
        }
        var totals = E("ICMSTot", new object[] { E("vBC", "0.00"), E("vICMS", "0.00"), E("vICMSDeson", "0.00"), E("vFCP", "0.00"), E("vBCST", "0.00"), E("vST", "0.00"), E("vFCPST", "0.00"), E("vFCPSTRet", "0.00"), E("vProd", F(brutos.Sum())), E("vFrete", F(request.Frete)), E("vSeg", "0.00"), E("vDesc", F(descontos.Sum() + request.Desconto)), E("vII", "0.00"), E("vIPI", "0.00"), E("vIPIDevol", "0.00"), E("vPIS", F(pisTotal)), E("vCOFINS", F(cofinsTotal)), E("vOutro", F(request.Despesas)), E("vNF", F(total)) });
        inf.Add(E("total", totals), E("transp", E("modFrete", request.ModalidadeFrete)));
        var pag = E("pag", "");
        foreach (var payment in request.Pagamentos)
        {
            var tPag = payment.Forma switch { "Pix" or "Pix (estatico)" => "20", "Dinheiro" => "01", "Cartão de crédito" => "03", "Cartão de débito" => "04", "Boleto" => "15", "Transferência" => "18", _ => throw new ArgumentException("Forma de pagamento nao suportada na NF-e.") };
            var detPag = E("detPag", new object[] { E("indPag", "0"), E("tPag", tPag), E("vPag", F(payment.Valor)) });
            if (tPag is "03" or "04") detPag.Add(E("card", E("tpIntegra", "2")));
            pag.Add(detPag);
        }
        inf.Add(pag);
        if (!string.IsNullOrWhiteSpace(request.Observacoes)) { Exigir(request.Observacoes.Length <= 500, "Observacoes devem ter ate 500 caracteres."); inf.Add(E("infAdic", E("infCpl", request.Observacoes.Trim()))); }
        var xml = NfeAssinatura.Assinar(new XElement(Ns + "NFe", inf), "infNFe", cert);
        ValidarSchema(xml);
        return new NotaFiscal { EmpresaId = empresa.Id, Numero = numero.ToString(), Serie = request.Serie.ToString(), Emissao = emissao.UtcDateTime, Cliente = cliente.NomeRazaoSocial, DocumentoCliente = documento, Tipo = "NFe", Ambiente = config.Ambiente, Situacao = "Processando", ChaveAcesso = chave, Valor = total, Xml = xml, XmlEnvioNfe = xml, UsuarioEmissorId = usuarioId, SolicitacaoId = request.SolicitacaoId,
            Itens = request.Itens.Select(i => new NotaFiscalItem { ProdutoId = i.ProdutoId, Quantidade = i.Quantidade, PrecoUnitario = i.PrecoUnitario, CustoUnitario = produtos[i.ProdutoId].Custo }).ToList() };
    }

    private static XElement Contribuicao(string name, string cst, decimal rate, decimal value, out decimal tax)
    {
        Exigir(Regex.IsMatch(cst ?? "", "^(0[1-9]|49|5[0-6]|6[0-7]|7[0-5]|98|99)$") && cst != "03", $"CST de {name} exige tributacao por quantidade ou nao e suportado.");
        tax = cst is "04" or "05" or "06" or "07" or "08" or "09" ? 0 : Moeda(value * rate / 100);
        var group = E(name + (cst is "01" or "02" ? "Aliq" : cst is "04" or "05" or "06" or "07" or "08" or "09" ? "NT" : "Outr"), E("CST", cst));
        if (!group.Name.LocalName.EndsWith("NT")) group.Add(E("vBC", F(value)), E("p" + name, F(rate, 4)), E("v" + name, F(tax)));
        return E(name, group);
    }

    private static decimal[] Ratear(decimal total, decimal[] bases)
    {
        var result = new decimal[bases.Length]; var remaining = total; var weight = bases.Sum();
        for (var i = 0; i < bases.Length; i++) { result[i] = i == bases.Length - 1 ? remaining : weight == 0 ? 0 : Moeda(remaining * bases[i] / weight); remaining -= result[i]; weight -= bases[i]; }
        return result;
    }
}