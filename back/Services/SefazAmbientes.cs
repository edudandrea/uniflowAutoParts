namespace back.Services;

public static class SefazAmbientes
{
    public static string UrlServico(string servico, int ambiente, string tipo = "NFe")
    {
        _ = Nome(ambiente);
        if (tipo is not ("NFe" or "NFCe")) throw new ArgumentException("Modelo fiscal invalido.");
        var host = (tipo == "NFe" ? "nfe" : "nfce") + (ambiente == 2 ? "-homologacao" : "");
        var path = servico switch
        {
            "NFeAutorizacao4" => "NfeAutorizacao/NFeAutorizacao4.asmx",
            "NFeRetAutorizacao4" => "NfeRetAutorizacao/NFeRetAutorizacao4.asmx",
            "NFeConsultaProtocolo4" => "NfeConsulta/NfeConsulta4.asmx",
            "NFeRecepcaoEvento4" => "recepcaoevento/recepcaoevento4.asmx",
            _ => throw new ArgumentException("Servico fiscal invalido.")
        };
        return $"https://{host}.sefazrs.rs.gov.br/ws/{path}";
    }
    public static int Codigo(string environment) => environment switch
    {
        "producao" => 1,
        "homologacao" => 2,
        _ => throw new ArgumentException("Ambiente fiscal invalido.")
    };

    public static string Nome(int ambiente) => ambiente switch
    {
        1 => "producao",
        2 => "homologacao",
        _ => throw new ArgumentException("Ambiente fiscal invalido.")
    };

    // Relacao oficial de servicos: https://dfe-portal.svrs.rs.gov.br/Nfe/Servicos
    public static string UrlStatus(int ambiente) => ambiente switch
    {
        1 => "https://nfe.sefazrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx",
        2 => "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx",
        _ => throw new ArgumentException("Ambiente fiscal invalido.")
    };

    public static string EnvelopeStatus(int ambiente)
    {
        _ = Nome(ambiente);
        return $"""
            <soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope">
              <soap12:Body>
                <nfeDadosMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4">
                  <consStatServ versao="4.00" xmlns="http://www.portalfiscal.inf.br/nfe"><tpAmb>{ambiente}</tpAmb><cUF>43</cUF><xServ>STATUS</xServ></consStatServ>
                </nfeDadosMsg>
              </soap12:Body>
            </soap12:Envelope>
            """;
    }
}