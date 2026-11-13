using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using back.Contracts;
using back.Controllers;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal static class NfeEmissionChecks
{
    public static async Task RunAsync()
    {
        var options = await NfeTestDatabase.CreateAsync(1, 2);
        TenantContext Tenant(int id) => new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(id) } });
        var checks = 0;
        void Check(bool v, string error) { if (!v) throw new Exception(error); checks++; }
        using var cert = NfeFixtures.Certificate();
        var encoded = Convert.ToBase64String(cert.Export(X509ContentType.Pfx, "fixture"));
        var credentials = new CertificadoRequest(encoded, "fixture");
        var transport = new FixtureSefaz();
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var company = await db.Empresas.SingleAsync(); var source = NfeFixtures.Empresa();
            company.Cnpj = source.Cnpj; company.RazaoSocial = source.RazaoSocial; company.InscricaoEstadual = source.InscricaoEstadual;
            company.Endereco = source.Endereco; company.Numero = source.Numero; company.Bairro = source.Bairro; company.Cidade = source.Cidade; company.Estado = source.Estado; company.Cep = source.Cep;
            db.ConfiguracoesFiscais.Add(NfeFixtures.Config()); db.ConfiguracoesTributarias.Add(NfeFixtures.Tax());
            var product = NfeFixtures.Produto(); product.Marca = new Marca { TenantId = 1, Nome = "Marca de teste" }; product.Categoria = new Categoria { TenantId = 1, Nome = "Categoria de teste" };
            db.CadastroItens.Add(product); db.Clientes.Add(NfeFixtures.Cliente()); await db.SaveChangesAsync();
        }
        EmissaoNfeController Controller(AppDbContext db, int tenant = 1) => new(db, Tenant(tenant), new NfeAutorizacaoService(transport), new SefazEventosService(transport));
        EmitirNfeRequest Request() => NfeFixtures.Request() with { Base64 = encoded, Password = "fixture" };
        EmissaoNfeResponse Result(IActionResult result) => result is OkObjectResult { Value: EmissaoNfeResponse response } ? response : throw new Exception("Emissao falhou: " + (result as ObjectResult)?.Value);
        var request = Request();
        EmissaoNfeResponse first;
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            first = Result(await Controller(db).Emitir(request, default));
            Check(first.Situacao == "Autorizada" && first.Ambiente == 1 && first.Protocolo.Length == 15, "Nao confirmou a autorizacao de producao.");
        }
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            Check((await db.Estoques.SingleAsync()).Quantidade == 2 && (await db.Estoques.SingleAsync()).Tipo == "Saida", "Autorizacao nao baixou o estoque.");
            Check((await db.NotasFiscais.SingleAsync()).Xml.Contains("nfeProc"), "Nao persistiu XML protocolado.");
            var downloads = new NotasFiscaisController(db, new SefazEventosService(transport), new NfeAutorizacaoService(transport));
            var xmlDownload = await downloads.Baixar(first.Id, "xml", 1, default) as FileContentResult;
            Check(xmlDownload is { ContentType: "application/xml" } && System.Text.Encoding.UTF8.GetString(xmlDownload.FileContents) == (await db.NotasFiscais.SingleAsync()).Xml, "Download alterou XML autorizado.");
            var pdfDownload = await downloads.Baixar(first.Id, "pdf", 1, default) as FileContentResult;
            Check(pdfDownload is { ContentType: "application/pdf" } && pdfDownload.FileContents.AsSpan().StartsWith("%PDF-"u8), "Nao gerou PDF de nota autorizada sem anexo.");
            using (var parsed = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(pdfDownload!.FileContents), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                Check(parsed.PageCount == 1 && parsed.Info.Subject.Contains(first.ChaveAcesso), "DANFE nao tem pagina ou chave da nota autorizada.");
            if (Environment.GetEnvironmentVariable("NFE_PDF_DIR") is { } pdfDir) await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(pdfDir, "danfe-authorized.pdf"), pdfDownload!.FileContents);
            var multipage = await db.NotasFiscais.SingleAsync();
            var expanded = NotaFiscalXml.Ler(multipage.Xml); var info = expanded.Descendants(NotaFiscalXml.Ns + "infNFe").Single();
            var productXml = info.Element(NotaFiscalXml.Ns + "det")!;
            for (var row = 2; row <= 140; row++) { var clone = new XElement(productXml); clone.SetAttributeValue("nItem", row); productXml.AddAfterSelf(clone); }
            foreach (var field in new[] { "vProd", "vNF" }) info.Element(NotaFiscalXml.Ns + "total")!.Element(NotaFiscalXml.Ns + "ICMSTot")!.Element(NotaFiscalXml.Ns + field)!.Value = "5600.00";
            var originalXml = multipage.Xml; multipage.Xml = expanded.ToString(SaveOptions.DisableFormatting);
            var many = NfeDanfeService.Gerar(multipage); multipage.Xml = originalXml;
            using (var parsed = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(many), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                Check(parsed.PageCount > 1, "DANFE com muitos itens nao paginou.");
            var replay = Result(await Controller(db).Emitir(request, default));
            Check(replay.Id == first.Id && transport.Authorizations == 1, "Repetir a mesma solicitacao retransmitiu a NF-e.");
            Check(await Controller(db).Emitir(request with { Observacoes = "Dados alterados" }, default) is ConflictObjectResult, "Reutilizou solicitacao com outros dados.");
            Check(await Controller(db).Emitir(request with { Csosn = "102" }, default) is ConflictObjectResult, "Reutilizou solicitacao com CSOSN alterado.");
            var eventos = new NotasFiscaisController(db, new SefazEventosService(transport), new NfeAutorizacaoService(transport));
            Check(await eventos.Cancelar(first.Id, 1, new("Cancelamento solicitado pelo cliente", encoded, "fixture"), default) is BadRequestObjectResult, "Cancelou sem confirmar nao circulacao.");
            Check(await eventos.Corrigir(first.Id, 1, new("Corrigir observacao de entrega no portao dois", encoded, "fixture"), default) is OkObjectResult, "Nao registrou CC-e de producao.");
            Check(await eventos.Corrigir(first.Id, 1, new("Corrigir entrega no portao dois e horario combinado", encoded, "fixture"), default) is OkObjectResult, "Nao registrou segunda CC-e.");
            Check(await db.NotaFiscalEventos.CountAsync(e => e.Situacao == "Confirmado") == 2 && transport.Events.Last().Sequence == 2, "CC-e nao incrementou sequencia.");
            Check(await eventos.Cancelar(first.Id, 1, new("Cancelamento solicitado pelo cliente", encoded, "fixture", true), default) is OkObjectResult, "Nao cancelou NF-e de producao.");
            var canceledPdf = await eventos.Baixar(first.Id, "pdf", 1, default) as FileContentResult;
            using (var parsed = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(canceledPdf!.FileContents), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                Check(parsed.Info.Title.Contains("CANCELADA"), "DANFE de nota cancelada nao identifica cancelamento.");
            if (Environment.GetEnvironmentVariable("NFE_PDF_DIR") is { } canceledDir) await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(canceledDir, "danfe-canceled.pdf"), canceledPdf!.FileContents);
            Check(await db.Estoques.CountAsync() == 2 && await db.Estoques.Where(e => e.Tipo == "Entrada").SumAsync(e => e.Quantidade) == 2, "Cancelamento nao estornou o estoque uma unica vez.");
            Check(await eventos.Consultar(first.Id, 1, credentials, default) is OkObjectResult && await db.Estoques.CountAsync() == 2, "Consulta duplicou estorno de estoque.");
            Check(await eventos.Cancelar(first.Id, 1, new("Cancelamento novamente solicitado", encoded, "fixture", true), default) is ConflictObjectResult, "Cancelou nota ja cancelada.");
            var eventoId = await db.NotaFiscalEventos.Where(e => e.Tipo == "110111").Select(e => e.Id).SingleAsync();
            Check(await eventos.BaixarEvento(first.Id, eventoId, 1, default) is FileContentResult, "Nao disponibilizou XML de evento confirmado.");
        }
        transport.TimeoutAfter = true;
        var timedOut = Request();
        EmissaoNfeResponse pending;
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            pending = Result(await Controller(db).Emitir(timedOut, default));
            Check(pending.Situacao == "Processando", "Timeout confirmou nota sem protocolo.");
            var downloads = new NotasFiscaisController(db, new SefazEventosService(transport), new NfeAutorizacaoService(transport));
            Check(await downloads.Baixar(pending.Id, "pdf", 1, default) is ConflictObjectResult && await downloads.Baixar(pending.Id, "xml", 1, default) is ConflictObjectResult, "Liberou download de nota sem autorizacao.");
            Check(await db.Estoques.CountAsync() == 3 && await db.Estoques.Where(e => e.NotaFiscalItem!.NotaFiscalId == pending.Id).SumAsync(e => e.Quantidade) == 2, "Venda pendente nao baixou a quantidade registrada.");
        }
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            Check(Result(await Controller(db).Emitir(timedOut, default)).Id == pending.Id && transport.Authorizations == 2, "Timeout gerou nova chave ao repetir solicitacao.");
            Check(Result(await Controller(db).Consultar(pending.Id, credentials, default)).Situacao == "Autorizada", "Consulta nao recuperou autorizacao apos timeout.");
            Check(await db.Estoques.CountAsync() == 3, "Recuperacao nao baixou estoque uma unica vez.");
            var eventos = new NotasFiscaisController(db, new SefazEventosService(transport), new NfeAutorizacaoService(transport));
            transport.TimeoutEventAfter = true;
            Check(await eventos.Corrigir(pending.Id, 1, new("Corrigir entrega no portao lateral da empresa", encoded, "fixture"), default) is ObjectResult { StatusCode: 502 }, "Timeout de evento nao manteve estado indeterminado.");
            Check(await eventos.Corrigir(pending.Id, 1, new("Corrigir entrega no portao lateral novamente", encoded, "fixture"), default) is ConflictObjectResult, "Permitiu outro evento sem reconciliar o anterior.");
            Check(await eventos.Consultar(pending.Id, 1, credentials, default) is OkObjectResult && await db.NotaFiscalEventos.AnyAsync(e => e.NotaFiscalId == pending.Id && e.Situacao == "Confirmado"), "Consulta nao recuperou CC-e aceita antes do timeout.");
            Check(await eventos.Consultar(pending.Id, 1, credentials, default) is OkObjectResult && await db.NotaFiscalEventos.CountAsync(e => e.NotaFiscalId == pending.Id) == 1, "Consulta duplicou evento recuperado.");
        }
        transport.TimeoutBefore = true;
        var lost = Request(); EmissaoNfeResponse notSent;
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            notSent = Result(await Controller(db).Emitir(lost, default));
            var query = Result(await Controller(db).Consultar(notSent.Id, credentials, default));
            Check(query.PodeReenviar && query.Codigo == "217", "Consulta de nota nao localizada nao habilitou reenvio do mesmo XML.");
            var resent = Result(await Controller(db).Reenviar(notSent.Id, credentials, default));
            Check(resent.Situacao == "Autorizada" && resent.ChaveAcesso == notSent.ChaveAcesso && resent.Numero == notSent.Numero, "Reenvio mudou chave ou numero.");
        }
        await using (var db = new AppDbContext(options, Tenant(2)))
        {
            Check(await Controller(db, 2).Consultar(first.Id, credentials, default) is NotFoundResult, "Consulta acessou nota de outra empresa.");
            var downloads = new NotasFiscaisController(db, new SefazEventosService(transport), new NfeAutorizacaoService(transport));
            Check(await downloads.Baixar(first.Id, "pdf", 2, default) is NotFoundObjectResult, "Download PDF acessou outra empresa.");
        }
        using var wrong = NfeFixtures.Certificate("98765432000199");
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var before = await db.NotasFiscais.CountAsync();
            Check(await Controller(db).Emitir(Request() with { Base64 = Convert.ToBase64String(wrong.Export(X509ContentType.Pfx, "fixture")) }, default) is BadRequestObjectResult, "Aceitou certificado de outro CNPJ-base.");
            Check(await db.NotasFiscais.CountAsync() == before, "Validacao invalida consumiu numero fiscal.");
        }
        transport.RejectNext = "203";
        var rejectedRequest = Request();
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var rejected = Result(await Controller(db).Emitir(rejectedRequest, default));
            Check(rejected.Situacao == "Rejeitada" && rejected.PodeReenviar, "Nota rejeitada com XML salvo nao permite reenvio.");
            var stored = await db.NotasFiscais.SingleAsync(n => n.Id == rejected.Id);
            var originalXml = stored.XmlEnvioNfe; var count = await db.NotasFiscais.CountAsync();
            var nextNumber = (await db.NumeracoesNfe.SingleAsync()).ProximoNumero;
            var before = transport.Authorizations;
            var resent = Result(await Controller(db).Reenviar(rejected.Id, credentials, default));
            Check(resent.Situacao == "Autorizada" && resent.Codigo == "100" && resent.Protocolo.Length == 15, "Reenvio nao retornou resposta e protocolo reais do transporte.");
            Check(resent.Id == rejected.Id && resent.ChaveAcesso == rejected.ChaveAcesso && resent.Numero == rejected.Numero && stored.XmlEnvioNfe == originalXml, "Reenvio alterou nota, chave, numero ou XML.");
            Check(await db.NotasFiscais.CountAsync() == count && (await db.NumeracoesNfe.SingleAsync()).ProximoNumero == nextNumber && transport.Authorizations == before + 1, "Reenvio criou nota ou consumiu nova numeracao.");
            Check(await Controller(db).Reenviar(rejected.Id, credentials, default) is ConflictObjectResult, "Permitiu reenvio de nota autorizada.");
            // Simula estado local desatualizado: a consulta deve recuperar autorizacao, sem retransmitir.
            stored.Situacao = "Rejeitada"; stored.CodigoAutorizacao = "203"; await db.SaveChangesAsync();
            var recovered = Result(await Controller(db).Reenviar(rejected.Id, credentials, default));
            Check(recovered.Situacao == "Autorizada" && transport.Authorizations == before + 1, "Retransmitiu nota ja autorizada na SEFAZ.");
        }
        transport.RejectNext = "203";
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var rejected = Result(await Controller(db).Emitir(Request(), default));
            var before = transport.Authorizations; transport.FailConsult = true;
            Check(await Controller(db).Reenviar(rejected.Id, credentials, default) is ObjectResult { StatusCode: 502 }, "Falha de consulta nao foi informada no reenvio.");
            Check(transport.Authorizations == before, "Transmitiu XML sem confirmar ausencia na SEFAZ.");
            transport.RejectNext = "225";
            var failed = Result(await Controller(db).Reenviar(rejected.Id, credentials, default));
            Check(failed.Situacao == "Rejeitada" && failed.Codigo == "225", "Perdeu rejeicao da SEFAZ no retorno do reenvio.");
        }
        await using (var db = new AppDbContext(options, Tenant(2)))
            Check(await Controller(db, 2).Reenviar(first.Id, credentials, default) is NotFoundResult, "Reenvio acessou outra empresa.");
        // Revisões fiscais pertencem à mesma venda: não repetir a baixa, ajustar quantidades e estornar ao cancelar.
        await using (var db = new AppDbContext(options, Tenant(1)))
        {
            var second = NfeFixtures.Produto(2); second.MarcaId = (await db.CadastroItens.FirstAsync()).MarcaId; second.CategoriaId = (await db.CadastroItens.FirstAsync()).CategoriaId;
            db.CadastroItens.Add(second); await db.SaveChangesAsync();
            decimal Net() => db.Estoques.AsEnumerable().Sum(e => e.Tipo == "Saida" ? e.Quantidade : -e.Quantidade);
            var baseline = Net();
            transport.RejectNext = "203";
            var venda = Request() with { Itens = [new(1, 3.5m, 20, 0), new(2, 2, 20, 0)], Pagamentos = [new("Pix", 110)] };
            var rejeitada = Result(await Controller(db).Emitir(venda, default));
            Check(rejeitada.Situacao == "Rejeitada" && Net() == baseline + 5.5m, "Venda registrada nao baixou quantidades fracionadas e todos os produtos.");
            var revision = venda with { SolicitacaoId = Guid.NewGuid(), VendaOrigemId = rejeitada.Id, Csosn = "102" };
            transport.RejectNext = "203";
            var revisada = Result(await Controller(db).Emitir(revision, default));
            Check(Net() == baseline + 5.5m, "Revisao fiscal repetiu a baixa da mesma venda.");
            Check(await Controller(db).Reenviar(rejeitada.Id, credentials, default) is ConflictObjectResult, "Reenviou tentativa substituida por revisao.");
            var reduced = revision with { SolicitacaoId = Guid.NewGuid(), Itens = [new(1, 1.25m, 20, 0)], Pagamentos = [new("Pix", 25)] };
            var aprovada = Result(await Controller(db).Emitir(reduced, default));
            Check(aprovada.Situacao == "Autorizada" && Net() == baseline + 1.25m, "Correcao da venda nao ajustou produto removido e quantidade reduzida.");
            var replay = Result(await Controller(db).Emitir(reduced, default));
            Check(replay.Id == aprovada.Id && Net() == baseline + 1.25m, "Repeticao da revisao alterou estoque.");
            Check(await Controller(db).Emitir(reduced with { SolicitacaoId = Guid.NewGuid() }, default) is ConflictObjectResult, "Criou outra revisao de venda autorizada.");
            var eventos = new NotasFiscaisController(db, new SefazEventosService(transport), new NfeAutorizacaoService(transport));
            transport.TimeoutEventAfter = true;
            Check(await eventos.Cancelar(aprovada.Id, 1, new("Cancelamento solicitado pelo cliente", encoded, "fixture", true), default) is ObjectResult { StatusCode: 502 } && Net() == baseline + 1.25m, "Estornou antes de confirmar o cancelamento na SEFAZ.");
            Check(await eventos.Consultar(aprovada.Id, 1, credentials, default) is OkObjectResult && Net() == baseline, "Cancelamento recuperado nao devolveu a quantidade restante da venda.");
            await eventos.Consultar(aprovada.Id, 1, credentials, default);
            Check(Net() == baseline, "Consulta repetida duplicou o estorno da venda revisada.");
        }
        Check(transport.Events.All(e => e.Environment == 1), "Eventos de producao foram enviados para homologacao.");
        Console.WriteLine($"NF-e authorization lifecycle: {checks} passed; certificado e transporte simulados, nenhuma transmissao externa.");
    }

    private sealed class FixtureSefaz : SefazTransporte
    {
        private readonly Dictionary<string, XElement> authorized = new();
        public readonly List<(string Key, int Environment, string Type, int Sequence, XElement Send, XElement Result)> Events = [];
        public string? RejectNext; public bool FailConsult;
        public int Authorizations; public bool TimeoutAfter; public bool TimeoutBefore; public bool TimeoutEventAfter;
        public override Task<XDocument> EnviarAsync(string service, int ambiente, string tipo, string xml, X509Certificate2 cert, CancellationToken token)
        {
            var ns = NotaFiscalXml.Ns; var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            if (service == "NFeAutorizacao4")
            {
                Authorizations++;
                if (TimeoutBefore) { TimeoutBefore = false; throw new HttpRequestException("Timeout simulado antes do processamento"); }
                var nfe = doc.Descendants(ns + "NFe").Single(); var key = nfe.Element(ns + "infNFe")!.Attribute("Id")!.Value[3..];
                if (RejectNext is { } rejection) { RejectNext = null; return Task.FromResult(NfeFixtures.Autorizacao(nfe, code: rejection, ambiente: ambiente)); }
                authorized[key] = new XElement(nfe);
                if (TimeoutAfter) { TimeoutAfter = false; throw new HttpRequestException("Timeout simulado apos autorizacao"); }
                return Task.FromResult(NfeFixtures.Autorizacao(nfe, ambiente: ambiente));
            }
            if (service == "NFeRecepcaoEvento4")
            {
                var evento = doc.Descendants(ns + "evento").Single(); var info = evento.Element(ns + "infEvento")!;
                var key = info.Element(ns + "chNFe")!.Value; var type = info.Element(ns + "tpEvento")!.Value; var seq = int.Parse(info.Element(ns + "nSeqEvento")!.Value);
                var result = new XElement(ns + "retEvento", new XAttribute("versao", "1.00"), new XElement(ns + "infEvento", new XElement(ns + "tpAmb", ambiente), new XElement(ns + "chNFe", key), new XElement(ns + "tpEvento", type), new XElement(ns + "nSeqEvento", seq), new XElement(ns + "cStat", "135"), new XElement(ns + "xMotivo", "Evento de teste confirmado"), new XElement(ns + "nProt", "143260000000002")));
                Events.Add((key, ambiente, type, seq, new XElement(evento), result));
                if (TimeoutEventAfter) { TimeoutEventAfter = false; throw new HttpRequestException("Timeout simulado apos registro do evento"); }
                return Task.FromResult(new XDocument(new XElement(ns + "retEnvEvento", new XElement(ns + "cStat", "128"), result)));
            }
            if (service == "NFeConsultaProtocolo4")
            {
                if (FailConsult) { FailConsult = false; throw new HttpRequestException("Falha simulada na consulta"); }
                var key = doc.Root!.Element(ns + "chNFe")!.Value;
                var result = new XElement(ns + "retConsSitNFe", new XElement(ns + "tpAmb", ambiente), new XElement(ns + "chNFe", key), new XElement(ns + "cStat", authorized.ContainsKey(key) ? Events.Any(e => e.Key == key && e.Type == "110111") ? "101" : "100" : "217"), new XElement(ns + "xMotivo", "Consulta de teste"));
                if (authorized.TryGetValue(key, out var nfe)) result.Add(NfeFixtures.Protocolo(nfe, ambiente: ambiente));
                foreach (var e in Events.Where(e => e.Key == key)) result.Add(new XElement(ns + "procEventoNFe", new XAttribute("versao", "1.00"), new XElement(e.Send), new XElement(e.Result)));
                return Task.FromResult(new XDocument(result));
            }
            throw new Exception("Servico nao simulado: " + service);
        }
    }
}
