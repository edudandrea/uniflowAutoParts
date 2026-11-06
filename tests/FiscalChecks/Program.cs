using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;
using back.Models;
using back.Services;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
}
void Reject(Action action, string message)
{
    try { action(); }
    catch (Exception e) when (e is ArgumentException or XmlException) { checks++; return; }
    throw new Exception(message);
}

// Somente fixtures locais; nenhuma chamada externa ou transmissao fiscal.
const string cnpj = "12345678000195";
const string chave = "43261112345678000195550010000000011123456780";
var ns = NotaFiscalXml.Ns;
var xml = $"""
<nfeProc versao="4.00" xmlns="{ns}"><NFe><infNFe Id="NFe{chave}" versao="4.00"><ide><mod>55</mod><tpNF>1</tpNF><nNF>1</nNF><serie>1</serie><dhEmi>2026-11-06T09:00:00-03:00</dhEmi><tpAmb>2</tpAmb></ide><emit><CNPJ>{cnpj}</CNPJ></emit><dest><xNome>Cliente de teste</xNome><CPF>12345678909</CPF></dest><total><ICMSTot><vNF>125.50</vNF></ICMSTot></total></infNFe></NFe><protNFe><infProt><tpAmb>2</tpAmb><chNFe>{chave}</chNFe><cStat>100</cStat><nProt>143260000000001</nProt></infProt></protNFe></nfeProc>
""";
var nota = NotaFiscalXml.Importar(xml, 1, "12.345.678/0001-95");
Check(nota.EmpresaId == 1 && nota.Valor == 125.50m && nota.Tipo == "NFe", "Importacao perdeu dados fiscais.");
Check(nota.Emissao == new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc), "Fuso horario incorreto.");
Check(nota.Xml == xml && nota.Pdf is null, "O XML original deve ser preservado e o PDF nao deve ser inventado.");
Reject(() => NotaFiscalXml.Importar(xml, 2, "00000000000000"), "Aceitou nota de outra empresa.");
Reject(() => NotaFiscalXml.Importar(xml.Replace("<tpNF>1", "<tpNF>0"), 1, cnpj), "Aceitou nota de entrada.");
Reject(() => NotaFiscalXml.Importar(xml.Replace("<cStat>100", "<cStat>301"), 1, cnpj), "Aceitou protocolo sem autorizacao.");
Reject(() => NotaFiscalXml.Ler("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///etc/passwd'>]><x>&a;</x>"), "Aceitou entidade externa.");

using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=Fiscal checks", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
var cancelXml = SefazEventosService.CriarEvento(nota, "110111", 1, "Cancelamento solicitado pelo cliente", cert);
var doc = new XmlDocument { PreserveWhitespace = true };
doc.LoadXml(cancelXml);
var signature = (XmlElement)doc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!;
var signed = new SignedXml(doc);
signed.LoadXml(signature);
Check(signed.CheckSignature(cert, true), "Assinatura do cancelamento invalida apos serializacao.");
var cancel = XDocument.Parse(cancelXml);
Check(cancel.Descendants(ns + "nProt").Single().Value == nota.Protocolo, "Cancelamento sem protocolo da nota.");
Check(cancel.Descendants(ns + "infEvento").Single().Attribute("Id")!.Value == "ID110111" + chave + "01", "ID do evento incorreto.");
var correcaoXml = SefazEventosService.CriarEvento(nota, "110110", 2, "Corrigir observacao para entrega no portao dois.", cert);
doc.LoadXml(correcaoXml);
signed = new SignedXml(doc);
signed.LoadXml((XmlElement)doc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!);
Check(signed.CheckSignature(cert, true), "Assinatura da carta de correcao invalida.");
Check(XDocument.Parse(correcaoXml).Descendants(ns + "nSeqEvento").Single().Value == "2", "Sequencia da CC-e incorreta.");
Reject(() => SefazEventosService.CriarEvento(nota, "110110", 21, "Correcao acima do limite de eventos.", cert), "Permitiu mais de 20 correcoes.");
Reject(() => SefazEventosService.CriarEvento(nota, "110111", 1, "Curto", cert), "Permitiu justificativa curta.");
nota.Tipo = "NFCe";
Reject(() => SefazEventosService.CriarEvento(nota, "110110", 1, "Correcao de uma NFC-e nao pode existir.", cert), "Permitiu carta de correcao em NFC-e.");
nota.Tipo = "NFe";
nota.Ambiente = 1;
Reject(() => SefazEventosService.CriarEvento(nota, "110111", 1, "Cancelamento de nota de producao.", cert), "Transmitiu nota de producao em homologacao.");
nota.Ambiente = 2;

string Retorno(string codigo, string protocolo = "143260000000002") => $"""
<retEnvEvento xmlns="{ns}"><cStat>128</cStat><retEvento><infEvento><tpAmb>2</tpAmb><chNFe>{chave}</chNFe><tpEvento>110111</tpEvento><nSeqEvento>1</nSeqEvento><cStat>{codigo}</cStat><xMotivo>Resultado de teste</xMotivo><nProt>{protocolo}</nProt></infEvento></retEvento></retEnvEvento>
""";
Check(SefazEventosService.LerRetorno(Retorno("135"), nota, "110111", 1).Confirmado, "Nao reconheceu confirmacao.");
Check(!SefazEventosService.LerRetorno(Retorno("128"), nota, "110111", 1).Confirmado, "Lote processado nao confirma evento.");
Check(!SefazEventosService.LerRetorno(Retorno("135", ""), nota, "110111", 1).Confirmado, "Aceitou confirmacao sem protocolo.");
Check(!SefazEventosService.LerRetorno(Retorno("573"), nota, "110111", 1).Confirmado, "Duplicidade nao confirma processamento local.");
Check(!SefazEventosService.LerRetorno(Retorno("136"), nota, "110111", 1).Confirmado, "Evento nao vinculado nao confirma cancelamento.");
var rejeitado = SefazEventosService.LerRetorno($"<retEnvEvento xmlns=\"{ns}\"><cStat>215</cStat><xMotivo>Falha no schema</xMotivo></retEnvEvento>", nota, "110111", 1);
Check(rejeitado.Codigo == "215" && !rejeitado.Confirmado, "Rejeicao de lote deve ser registrada sem simular confirmacao.");
Reject(() => SefazEventosService.LerRetorno(Retorno("135"), nota, "110110", 1), "Aceitou retorno de outro tipo de evento.");
Reject(() => SefazEventosService.LerRetorno(Retorno("135"), nota, "110111", 2), "Aceitou retorno de outra sequencia.");
Reject(() => SefazEventosService.LerRetorno(Retorno("135").Replace("<tpAmb>2", "<tpAmb>1"), nota, "110111", 1), "Aceitou retorno de outro ambiente.");
Console.WriteLine($"Fiscal checks: {checks} passed. Nenhuma chamada externa realizada.");
