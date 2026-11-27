using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using back.Controllers;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Banco descartavel e autenticacao exclusiva deste executavel de testes.
// Nenhum endpoint, chave ou bypass de autenticacao e adicionado a aplicacao real.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var root = new InMemoryDatabaseRoot();
var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("tenant-checks", root).Options;
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<TenantActionFilter>();
builder.Services.AddDbContext<AppDbContext>(b => b.UseInMemoryDatabase("tenant-checks", root));
builder.Services.AddSingleton(new HubAuthSettings(new ConfigurationBuilder().Build(), builder.Environment));
builder.Services.AddScoped<SefazEventosService>();
builder.Services.AddScoped<SefazTransporte>();
builder.Services.AddScoped<NfeAutorizacaoService>();
builder.Services.AddScoped<EmpresaLogoService>();
builder.Services.AddSingleton<ICloudflareR2StorageService, ChecksStorage>();
builder.Services.AddAntiforgery(o => o.HeaderName = "X-Parts-CSRF");
builder.Services.AddAuthentication("Checks").AddScheme<AuthenticationSchemeOptions, ChecksAuthentication>("Checks", _ => { });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder("Checks").RequireAuthenticatedUser().Build());
builder.Services.AddControllers(o => { o.Filters.AddService<TenantActionFilter>(); o.Filters.Add<CsrfValidationFilter>(); })
    .AddApplicationPart(typeof(ProdutosController).Assembly);
await using var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
await app.StartAsync();
using var client = app.GetTestClient();
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
var roundingProduct = new CadastroItens { Custo = 12.5, MargemPreco = 4.2m };
roundingProduct.AtualizarPrecoPelaMargem();
Check(roundingProduct.Preco == 13.03, "Arredondamento de meio centavo nao respeitou o preco de venda.");
roundingProduct.Custo = 20;
roundingProduct.AtualizarPrecoPelaMargem();
Check(roundingProduct.Preco == 20.84 && roundingProduct.MargemPreco == 4.2m,
    "Atualizacao de custo perdeu a margem definida no produto.");
await HubRedirectChecks.RunAsync();
await HubCompanyProfileChecks.RunAsync();
await StockPostgresChecks.RunAsync();
await NfeImportChecks.RunAsync();
await NfeProductChecks.RunAsync();
await NfeEmissionChecks.RunAsync();
await CustomerFiscalAddressChecks.RunAsync();
await CustomerEditorChecks.RunAsync();
TenantContext Tenant(int id) => new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(id) } });

var directoryOptions = new DbContextOptionsBuilder<HubDirectoryDbContext>().UseInMemoryDatabase("tenant-checks", root).Options;
await using (var directory = new HubDirectoryDbContext(directoryOptions))
{
    directory.Empresas.AddRange(new Empresas { Id = 1, RazaoSocial = "EMPRESA A", NomeFantasia = "EMPRESA A", Cnpj = "11111111000111", LogoUrl = "https://example.test/empresa-a.png" },
        new Empresas { Id = 2, RazaoSocial = "EMPRESA B", NomeFantasia = "EMPRESA B", Cnpj = "22222222000122" });
    directory.Users.AddRange(new Users { Id = 1, EmpresaId = 1, Nome = "Usuario A", Email = "a@example.test", EmailNormalizado = "A@EXAMPLE.TEST" },
        new Users { Id = 2, EmpresaId = 2, Nome = "Usuario B", Email = "b@example.test", EmailNormalizado = "B@EXAMPLE.TEST" });
    await directory.SaveChangesAsync();
}
for (var tenantId = 1; tenantId <= 2; tenantId++)
{
    await using var db = new AppDbContext(options, Tenant(tenantId));
    db.Categorias.Add(new Categoria { Id = tenantId, TenantId = tenantId, Nome = $"Categoria {tenantId}" });
    db.Marcas.Add(new Marca { Id = tenantId, TenantId = tenantId, Nome = $"Marca {tenantId}" });
    db.CadastroItens.Add(new CadastroItens { Id = tenantId, EmpresaId = tenantId, SKU = tenantId, Descricao = $"Produto {tenantId}", Custo = 100, MarcaId = tenantId, CategoriaId = tenantId, Ativo = true });
    db.Clientes.Add(new Cliente { Id = tenantId, TenantId = tenantId, NomeRazaoSocial = $"Cliente {tenantId}", CpfCnpj = $"0000000000{tenantId}" });
    db.ClienteContatos.Add(new ClienteContato { Id = tenantId, ClienteId = tenantId, Nome = $"Contato {tenantId}" });
    db.Estoques.Add(new Estoque { Id = tenantId, EmpresaId = tenantId, ItemId = tenantId, UsuarioId = tenantId, Quantidade = tenantId * 5, Tipo = "Entrada" });
    db.NotasFiscais.Add(new NotaFiscal { Id = tenantId, EmpresaId = tenantId, Numero = tenantId.ToString(), ChaveAcesso = new string((char)(48 + tenantId), 44), Xml = $"<nfeProc>Empresa {tenantId}</nfeProc>", Pdf = [37, 80, 68, 70, 45], Ambiente = 2 });
    db.EntradasCompra.Add(new EntradaCompra { Id = tenantId, TenantId = tenantId, FornecedorId = tenantId,
        ChaveNfe = new string((char)(48 + tenantId), 44), NumeroNfe = tenantId.ToString(),
        Itens = [new EntradaCompraItem { NumeroItem = 1, DescricaoXml = "Peca da empresa " + tenantId, QuantidadeComercial = 2, QuantidadeRecebida = 2 }] });
    await db.SaveChangesAsync();
}
Check((await client.GetAsync("/api/produtos?empresaId=1")).StatusCode == HttpStatusCode.Unauthorized, "API aceitou leitura sem autenticacao.");
for (var tenantId = 1; tenantId <= 2; tenantId++)
{
    client.DefaultRequestHeaders.Remove("Test-Company");
    client.DefaultRequestHeaders.Add("Test-Company", tenantId.ToString());
    var productResponse = await client.GetAsync("/api/produtos");
    if (!productResponse.IsSuccessStatusCode) throw new Exception(await productResponse.Content.ReadAsStringAsync());
    var products = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
    Check(products.GetProperty("total").GetInt32() == 1 && products.GetProperty("produtos")[0].GetProperty("empresaId").GetInt32() == tenantId, "Produtos nao isolados por empresa.");
    var stock = await client.GetFromJsonAsync<JsonElement>("/api/estoque");
    Check(stock.GetArrayLength() == 1 && stock[0].GetProperty("estoque").GetDecimal() == tenantId * 5, "Saldo de estoque vazou entre empresas.");
    var customers = await client.GetFromJsonAsync<JsonElement>("/api/clientes");
    Check(customers.GetArrayLength() == 1 && customers[0].GetProperty("empresaId").GetInt32() == tenantId, "Clientes nao isolados.");
    var entradas = await client.GetFromJsonAsync<JsonElement>("/api/estoque/entradas");
    Check(entradas.GetArrayLength() == 1 && entradas[0].GetProperty("entradaCompraId").GetInt64() == tenantId,
        "Listagem de conferencias vazou entre empresas.");
    Check((await client.GetAsync($"/api/estoque/entradas/{3 - tenantId}")).StatusCode == HttpStatusCode.NotFound,
        "Leitura de conferencia acessou nota de outra empresa.");
    Check((await client.GetAsync($"/api/estoque/entradas/{tenantId}")).StatusCode == HttpStatusCode.OK,
        "Nao reabriu nota importada da propria empresa.");
    Check((await client.GetAsync($"/api/produtos?empresaId={3 - tenantId}")).StatusCode == HttpStatusCode.Forbidden, "Aceitou empresa adulterada na URL.");
    var sessionResponse = await client.GetAsync("/api/auth/session");
    var session = (await sessionResponse.Content.ReadFromJsonAsync<JsonElement>());
    Check(session.GetProperty("empresa").GetProperty("id").GetInt32() == tenantId, "Perfil exibiu empresa incorreta.");
    Check(session.GetProperty("empresa").GetProperty("logoUrl").GetString() == (tenantId == 1 ? "https://example.test/empresa-a.png" : null), "Sessao nao isolou a logo por empresa.");
    Check(session.GetProperty("usuario").GetProperty("nome").GetString() == $"Usuario {tenantId}", "Perfil exibiu usuario incorreto.");
    Check(sessionResponse.Headers.CacheControl?.NoStore == true, "Sessao nao deve ser armazenada em cache.");
    Check((await client.GetAsync($"/api/notas-fiscais/{3 - tenantId}/xml")).StatusCode == HttpStatusCode.NotFound, "Download XML acessou nota alheia.");
    Check((await client.GetAsync($"/api/notas-fiscais/{3 - tenantId}/pdf")).StatusCode == HttpStatusCode.NotFound, "Download PDF acessou nota alheia.");
    Check((await client.GetAsync($"/api/notas-fiscais/{tenantId}/xml")).StatusCode == HttpStatusCode.OK, "Download de XML proprio falhou.");
    Check((await client.GetAsync($"/api/documentos/empresas/{3 - tenantId}/arquivo?key=empresas/{3 - tenantId}/logo.png")).StatusCode == HttpStatusCode.Forbidden, "Download aceitou rota de armazenamento de outra empresa.");
    var payload = new { tenantId = 3 - tenantId, nome = "Marca adulterada", ativo = true };
    Check((await client.PostAsJsonAsync("/api/marcas", payload)).StatusCode == HttpStatusCode.BadRequest, "Escrita aceitou requisicao sem CSRF.");
    if (sessionResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies.Select(c => c.Split(';')[0])));
    }
    client.DefaultRequestHeaders.Remove("X-Parts-CSRF");
    client.DefaultRequestHeaders.Add("X-Parts-CSRF", session.GetProperty("csrfToken").GetString());
    const string municipiosUrl = "/api/municipios";
    var municipios = await client.GetFromJsonAsync<MunicipioResponse[]>(municipiosUrl) ?? [];
    Check(municipios.Length == 5571 && municipios.Select(m => m.Uf).Distinct().Count() == 27 &&
        municipios.Any(m => m is { CodigoIbge: "5101837", Nome: "Boa Esperança do Norte", Uf: "MT" }),
        "Catálogo IBGE incompleto ou cadastro manual visível a outra empresa.");
    if (tenantId == 1)
    {
        var manual = new MunicipioResponse("5199999", "Município de teste", "MT");
        Check((await client.PostAsJsonAsync(municipiosUrl, manual with { Uf = "SP" })).StatusCode == HttpStatusCode.BadRequest,
            "Cadastro aceitou código IBGE de outra UF.");
        Check((await client.PostAsJsonAsync(municipiosUrl, manual with { CodigoIbge = "5101837" })).StatusCode == HttpStatusCode.Conflict,
            "Cadastro permitiu duplicar código da base IBGE.");
        using var cadastro = await client.PostAsJsonAsync(municipiosUrl, manual);
        Check(cadastro.StatusCode == HttpStatusCode.Created,
            $"Não cadastrou município manual ({cadastro.StatusCode}): {await cadastro.Content.ReadAsStringAsync()}");
        Check((await client.PostAsJsonAsync(municipiosUrl, manual)).StatusCode == HttpStatusCode.Conflict,
            "Cadastro permitiu duplicar município manual.");
        municipios = await client.GetFromJsonAsync<MunicipioResponse[]>(municipiosUrl) ?? [];
        Check(municipios.Length == 5572 && municipios.Contains(manual), "Município manual não apareceu para sua empresa.");
    }
    var fiscalProfileResponse = await client.GetAsync($"/api/fiscal/destinatario/{tenantId}");
    Check(fiscalProfileResponse.IsSuccessStatusCode && fiscalProfileResponse.Headers.CacheControl?.NoStore == true, "Consulta fiscal permitiu cache de dados desatualizados.");
    var customerDetail = await client.GetFromJsonAsync<back.Contracts.ClienteDetalheResponse>($"/api/clientes/{tenantId}");
    Check(customerDetail?.Id == tenantId && customerDetail.Dados.Contatos?.Count == 1, "Consulta HTTP perdeu dados completos do cliente.");
    var customerUpdate = customerDetail!.Dados with { TipoPessoa = TipoPessoa.fisica, IndicadorIe = IndicadorIe.NaoContribuinte, NomeRazaoSocial = $"Cliente editado {tenantId}" };
    Check((await client.PutAsJsonAsync($"/api/clientes/{tenantId}", customerUpdate)).StatusCode == HttpStatusCode.OK, "Edicao HTTP do proprio cliente falhou.");
    Check((await client.GetAsync($"/api/clientes/{3 - tenantId}")).StatusCode == HttpStatusCode.NotFound, "Consulta HTTP leu cliente alheio.");
    Check((await client.PutAsJsonAsync($"/api/clientes/{3 - tenantId}", customerUpdate)).StatusCode == HttpStatusCode.NotFound, "Edicao HTTP alterou cliente alheio.");
    Check((await client.PutAsJsonAsync($"/api/clientes/{tenantId}", customerUpdate with { EmpresaId = 3 - tenantId })).StatusCode == HttpStatusCode.Forbidden, "Edicao HTTP aceitou empresa adulterada.");
    Check((await client.GetAsync("/api/fiscal/tributacao")).StatusCode == HttpStatusCode.NoContent,
        "Empresa sem tributacao recebeu configuracao de outra empresa.");
    var defaultEnvironment = await client.GetFromJsonAsync<JsonElement>("/api/fiscal/ambiente");
    Check(defaultEnvironment.GetProperty("environment").GetString() == "homologacao",
        "Ambiente padrao de uma empresa foi alterado pela configuracao de outra empresa.");
    var desiredEnvironment = tenantId == 1 ? "producao" : "homologacao";
    Check((await client.PutAsJsonAsync("/api/fiscal/ambiente", new { environment = desiredEnvironment })).StatusCode == HttpStatusCode.OK,
        "Nao salvou o ambiente fiscal da empresa autenticada.");
    Check((await client.PutAsJsonAsync("/api/fiscal/ambiente", new { environment = desiredEnvironment })).StatusCode == HttpStatusCode.OK,
        "Nao atualizou a configuracao de ambiente existente.");
    var savedEnvironment = await client.GetFromJsonAsync<JsonElement>("/api/fiscal/ambiente");
    Check(savedEnvironment.GetProperty("environment").GetString() == desiredEnvironment && savedEnvironment.GetProperty("emissaoDisponivel").GetBoolean(),
        "Configuracao de ambiente nao persistiu ou nao disponibilizou o fluxo de emissao.");
    Check((await client.PutAsJsonAsync("/api/fiscal/ambiente", new { environment = "invalido" })).StatusCode == HttpStatusCode.BadRequest,
        "Aceitou um ambiente fiscal invalido.");
    var taxSettings = new { regime = "1", icmsCode = "101", pisCode = "49", cofinsCode = "49",
        icmsRate = (decimal?)null, pisRate = tenantId + 0.1234m, cofinsRate = 0m };
    Check((await client.PutAsJsonAsync("/api/fiscal/tributacao", taxSettings)).StatusCode == HttpStatusCode.OK,
        "Nao salvou tributacao da empresa autenticada.");
    Check((await client.PutAsJsonAsync("/api/fiscal/tributacao", taxSettings)).StatusCode == HttpStatusCode.OK,
        "Nao atualizou tributacao existente.");
    var taxRead = await client.GetFromJsonAsync<JsonElement>("/api/fiscal/tributacao");
    Check(taxRead.GetProperty("pisRate").GetDecimal() == tenantId + 0.1234m && taxRead.GetProperty("icmsRate").ValueKind == JsonValueKind.Null,
        "Leitura perdeu aliquotas ou acessou tributacao de outra empresa.");
    Check((await client.PutAsJsonAsync("/api/fiscal/tributacao", new { regime = "1", icmsCode = "00", pisCode = "49", cofinsCode = "49", pisRate = 1 })).StatusCode == HttpStatusCode.BadRequest,
        "Tributacao aceitou codigo incompativel com o regime.");
    Check((await client.PutAsJsonAsync("/api/fiscal/tributacao", new { regime = "1", icmsCode = "101", pisCode = "49", cofinsCode = "49", pisRate = 101 })).StatusCode == HttpStatusCode.BadRequest,
        "Tributacao aceitou aliquota invalida.");
    var marginSave = await client.PutAsJsonAsync($"/api/estoque/margem", new { margemPreco = 30m });
    Check(marginSave.StatusCode == HttpStatusCode.OK,
        "Nao salvou a margem no produto da empresa autenticada: " + await marginSave.Content.ReadAsStringAsync());
    var pricedStock = await client.GetFromJsonAsync<JsonElement>("/api/estoque");
    Check(pricedStock[0].GetProperty("custo").GetDouble() == 100 && pricedStock[0].GetProperty("valor").GetDouble() == 130 &&
        pricedStock[0].GetProperty("margemPreco").GetDecimal() == 30,
        "Margem de 30% sobre custo de 100 nao resultou em preco de venda 130 persistido.");
    var saleCatalog = await client.GetFromJsonAsync<JsonElement>("/api/produtos");
    Check(saleCatalog.GetProperty("produtos")[0].GetProperty("preco").GetDouble() == 130,
        "O catalogo de vendas nao recebeu o preco calculado pela margem.");
    Check((await client.PutAsJsonAsync($"/api/estoque/{tenantId}/margem", new { margemPreco = 50m })).StatusCode == HttpStatusCode.Conflict,
        "A rota antiga ainda permitiu margem por item.");
    Check((await client.PutAsJsonAsync($"/api/estoque/margem?empresaId={3 - tenantId}", new { margemPreco = 50m })).StatusCode == HttpStatusCode.Forbidden,
        "Margem aceitou empresa adulterada na URL.");
    foreach (var invalidMargin in new decimal?[] { -1m, null, 100000m, 1.12345m })
        Check((await client.PutAsJsonAsync($"/api/estoque/margem", new { margemPreco = invalidMargin })).StatusCode == HttpStatusCode.BadRequest,
            "Margem invalida foi aceita.");
    Check((await client.PutAsJsonAsync($"/api/estoque/margem", new { margemPreco = 33.3333m })).StatusCode == HttpStatusCode.OK,
        "Margem fracionaria nao foi aceita.");
    var roundedStock = await client.GetFromJsonAsync<JsonElement>("/api/estoque");
    Check(roundedStock[0].GetProperty("valor").GetDouble() == 133.33, "Preco de venda nao foi arredondado para centavos.");
    Check((await client.PutAsJsonAsync($"/api/estoque/margem", new { margemPreco = 0m })).StatusCode == HttpStatusCode.OK,
        "Margem de zero explicitamente definida nao foi aceita.");
    var conferencia = new { itens = new[] { new { numeroItem = 1, produtoId = tenantId, quantidadeRecebida = 1.5m } }, concluir = false };
    Check((await client.PutAsJsonAsync($"/api/estoque/entradas/{3 - tenantId}/conferencia", conferencia)).StatusCode == HttpStatusCode.NotFound,
        "Conferencia alterou nota de outra empresa.");
    var salva = await client.PutAsJsonAsync($"/api/estoque/entradas/{tenantId}/conferencia", conferencia);
    Check(salva.StatusCode == HttpStatusCode.OK, "Nao salvou conferencia propria: " + await salva.Content.ReadAsStringAsync());
    var reaberta = await client.GetFromJsonAsync<JsonElement>($"/api/estoque/entradas/{tenantId}");
    Check(reaberta.GetProperty("itens")[0].GetProperty("quantidadeRecebida").GetDecimal() == 1.5m,
        "Reabrir a nota perdeu quantidade conferida pela API.");
    var foreignWrite = await client.PostAsJsonAsync("/api/marcas", payload);
    Check(foreignWrite.StatusCode == HttpStatusCode.Forbidden, "Escrita com empresa adulterada retornou " + foreignWrite.StatusCode + ": " + await foreignWrite.Content.ReadAsStringAsync());
    Check((await client.PutAsJsonAsync($"/api/marcas/{3 - tenantId}", new { tenantId, nome = "Marca alheia", ativo = true })).StatusCode == HttpStatusCode.NotFound, "Atualizacao acessou registro de outra empresa pelo ID.");
    var own = await client.PostAsJsonAsync("/api/marcas", new { tenantId, nome = $"Marca propria {tenantId}", ativo = true });
    Check(own.StatusCode == HttpStatusCode.Created, "Nao conseguiu criar cadastro na propria empresa: " + await own.Content.ReadAsStringAsync());
    await using var db = new AppDbContext(options, Tenant(tenantId));
    Check(await db.ClienteContatos.CountAsync() == 1, "Filtro de tabela filha vazou contatos de outra empresa.");
    Check((await db.Marcas.ToListAsync()).All(m => m.TenantId == tenantId), "Consulta sem Where vazou marcas.");
    db.ClienteContatos.Add(new ClienteContato { Nome = "Contato alheio", ClienteId = 3 - tenantId });
    try { await db.SaveChangesAsync(); throw new Exception("Gravou FK apontando para outro cliente/empresa."); }
    catch (TenantAccessException) { checks++; }
    db.ChangeTracker.Clear();
    db.Marcas.Attach(new Marca { Id = 3 - tenantId, TenantId = tenantId, Nome = "Update forjado" });
    db.Entry(db.Marcas.Local.Single()).Property(m => m.Nome).IsModified = true;
    try { await db.SaveChangesAsync(); throw new Exception("Update por Attach burlou o isolamento."); }
    catch (TenantAccessException) { checks++; }
}
client.DefaultRequestHeaders.Remove("Test-Company");
client.DefaultRequestHeaders.Add("Test-Company", "1");
var marginSession = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
client.DefaultRequestHeaders.Remove("X-Parts-CSRF");
client.DefaultRequestHeaders.Add("X-Parts-CSRF", marginSession.GetProperty("csrfToken").GetString());
var globalRead = await client.GetFromJsonAsync<JsonElement>("/api/estoque/margem");
Check(globalRead.GetProperty("margemPreco").GetDecimal() == 0, "Margem global de zero nao persistiu.");
Check((await client.PutAsJsonAsync("/api/estoque/margem", new { margemPreco = 30m })).StatusCode == HttpStatusCode.OK, "Nao definiu margem geral.");
await using (var db = new AppDbContext(options, Tenant(1)))
{
    db.CadastroItens.AddRange(new CadastroItens { EmpresaId = 1, SKU = 9001, Descricao = "Novo produto com margem geral", Custo = 12.5, Preco = 999, MarcaId = 1, CategoriaId = 1 },
        new CadastroItens { EmpresaId = 1, SKU = 9002, Descricao = "Produto inativo sem saldo", Custo = 50, MarcaId = 1, CategoriaId = 1, Ativo = false });
    await db.SaveChangesAsync();
}
await using (var db = new AppDbContext(options, Tenant(1)))
{
    Check((await db.CadastroItens.SingleAsync(p => p.SKU == 9001)).Preco == 16.25, "Novo cadastro nao herdou a margem geral.");
    Check((await db.CadastroItens.SingleAsync(p => p.SKU == 9002)).Preco == 65, "Cadastro sem saldo nao recebeu a margem geral.");
}
var globalApply = await client.PutAsJsonAsync("/api/estoque/margem", new { margemPreco = 50m });
Check(globalApply.StatusCode == HttpStatusCode.OK && (await globalApply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("produtosAtualizados").GetInt32() == 3, "A margem global nao atingiu todos os produtos.");
await using (var db = new AppDbContext(options, Tenant(1)))
{
    Check((await db.CadastroItens.ToListAsync()).All(p => p.MargemPreco == 50m && p.Preco == (double)decimal.Round((decimal)p.Custo * 1.5m, 2, MidpointRounding.AwayFromZero)), "A margem nao recalculou todo o estoque, inclusive inativos.");
    var changed = await db.CadastroItens.SingleAsync(p => p.SKU == 9001); changed.Custo = 22.5; changed.Preco = 1;
    await db.SaveChangesAsync();
}
await using (var db = new AppDbContext(options, Tenant(1)))
{
    Check((await db.CadastroItens.SingleAsync(p => p.SKU == 9001)).Preco == 33.75, "Alteracao de custo perdeu a margem global.");
    Check((await db.NotasFiscais.SingleAsync()).Valor == 0, "Recalculo de precos alterou documento fiscal existente.");
}
await using (var db = new AppDbContext(options, Tenant(2)))
    Check((await db.Empresas.SingleAsync()).MargemPrecoEstoque == 0 && (await db.CadastroItens.SingleAsync()).MargemPreco == 0, "Margem geral de A alterou o cadastro de B.");
var persistedEnvironment = await client.GetFromJsonAsync<JsonElement>("/api/fiscal/ambiente");
Check(persistedEnvironment.GetProperty("environment").GetString() == "producao", "Empresa B alterou o ambiente de producao salvo para empresa A.");
var persistedTax = await client.GetFromJsonAsync<JsonElement>("/api/fiscal/tributacao");
Check(persistedTax.GetProperty("pisRate").GetDecimal() == 1.1234m,
    "Salvar tributacao da empresa B sobrescreveu a empresa A.");
await using (var anonymous = new AppDbContext(options))
{
    Check(await anonymous.CadastroItens.CountAsync() == 0, "Contexto sem empresa leu dados.");
    anonymous.Marcas.Add(new Marca { TenantId = 1, Nome = "Sem sessao" });
    try { await anonymous.SaveChangesAsync(); throw new Exception("Contexto sem sessao gravou dados."); }
    catch (TenantAccessException) { checks++; }
}
var validPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "central-7"), new Claim("tenant_id", "central-3"), new Claim("project_id", "2")], "Checks"));
Check(HubAuthentication.ValidProjectClaims(validPrincipal, 2), "Rejeitou claims do proprio projeto.");
Check(!HubAuthentication.ValidProjectClaims(validPrincipal, 3), "Aceitou token destinado a outro projeto.");
((ClaimsIdentity)validPrincipal.Identity!).RemoveClaim(validPrincipal.FindFirst("tenant_id")!);
Check(!HubAuthentication.ValidProjectClaims(validPrincipal, 2), "Aceitou identidade sem empresa.");
// Virada do mês em São Paulo, exclusão de rascunhos e isolamento entre empresas.
var limiteFinanceiro = new DateTime(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc);
foreach (var empresaId in new[] { 1, 2 })
{
    await using var db = new AppDbContext(options, Tenant(empresaId));
    db.NotasFiscais.AddRange(
        new NotaFiscal { EmpresaId = empresaId, Ambiente = 1, Numero = "100", Emissao = limiteFinanceiro,
            Valor = empresaId * 100, Situacao = "Autorizada" },
        new NotaFiscal { EmpresaId = empresaId, Ambiente = 1, Numero = "101", Emissao = limiteFinanceiro.AddSeconds(-1),
            Valor = empresaId * 20, Situacao = "Autorizada" },
        new NotaFiscal { EmpresaId = empresaId, Ambiente = 1, Numero = "102", Emissao = limiteFinanceiro,
            Valor = 900, Situacao = "Cancelada" },
        new NotaFiscal { EmpresaId = empresaId, Ambiente = 2, Numero = "103", Emissao = limiteFinanceiro, Valor = 900 });
    db.EntradasCompra.AddRange(
        new EntradaCompra { TenantId = empresaId, FornecedorId = empresaId, NumeroNfe = "200", DataEmissao = limiteFinanceiro.AddHours(1),
            Status = "Conferida", ValorTotal = empresaId * 35 },
        new EntradaCompra { TenantId = empresaId, NumeroNfe = "201", DataEmissao = limiteFinanceiro, ValorTotal = 900 });
    await db.SaveChangesAsync();
}
foreach (var empresaId in new[] { 1, 2 })
{
    client.DefaultRequestHeaders.Remove("Test-Company");
    client.DefaultRequestHeaders.Add("Test-Company", empresaId.ToString());
    const string financeiroUrl = "/api/financeiro/resumo?mes=2026-01-01";
    var financeiro = await client.GetFromJsonAsync<JsonElement>(financeiroUrl);
    var serie = financeiro.GetProperty("serie");
    var mes = serie[5];
    Check(serie.GetArrayLength() == 6 && serie[4].GetProperty("saidas").GetDecimal() == empresaId * 20
        && mes.GetProperty("saidas").GetDecimal() == empresaId * 100 && mes.GetProperty("totalSaidas").GetInt32() == 1
        && mes.GetProperty("compras").GetDecimal() == empresaId * 35 && mes.GetProperty("totalCompras").GetInt32() == 1,
        "Financeiro somou documentos de outro mês, empresa, situação ou ambiente.");
    var documentos = financeiro.GetProperty("documentos");
    Check(documentos.GetArrayLength() == 2 && documentos[0].GetProperty("numero").GetString() == "200"
        && documentos[0].GetProperty("valor").GetDecimal() == empresaId * 35
        && documentos[1].GetProperty("numero").GetString() == "100", "Documentos recentes fora da ordem ou do filtro fiscal.");
    Check((await client.GetAsync($"{financeiroUrl}&empresaId={3 - empresaId}")).StatusCode == HttpStatusCode.Forbidden,
        "Financeiro aceitou consultar outra empresa.");
}

foreach (var empresaId in new[] { 1, 2 })
{
    await using var db = new AppDbContext(options, Tenant(empresaId));
    EntradaCompra CompraComDuplicatas(int numero, string status, int ambiente)
    {
        var chave = $"{empresaId}{numero}".PadLeft(44, '0');
        return new EntradaCompra
        {
            TenantId = empresaId, FornecedorId = empresaId, ChaveNfe = chave, NumeroNfe = numero.ToString(),
            Serie = "1", NaturezaOperacao = "Compra de mercadorias", Status = status,
            XmlOriginal = $"""
                <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe"><NFe><infNFe Id="NFe{chave}">
                  <ide><tpAmb>{ambiente}</tpAmb></ide>
                  <cobr>
                    <dup><nDup>001</nDup><dVenc>2026-12-05</dVenc><vDup>{empresaId}00.50</vDup></dup>
                    <dup><nDup>002</nDup><dVenc>2027-01-05</dVenc><vDup>35.00</vDup></dup>
                    <dup><nDup>003</nDup><vDup>35.00</vDup></dup>
                  </cobr>
                </infNFe></NFe><protNFe><infProt>
                  <tpAmb>{ambiente}</tpAmb><chNFe>{chave}</chNFe><nProt>135260000000001</nProt><cStat>100</cStat>
                </infProt></protNFe></nfeProc>
                """
        };
    }
    db.EntradasCompra.AddRange(CompraComDuplicatas(500, "Conferida", 1),
        CompraComDuplicatas(501, "Conferida", 2), CompraComDuplicatas(502, "Conferencia", 1),
        new EntradaCompra { TenantId = empresaId, Status = "Conferida", XmlOriginal = "<cobr><dVenc>" });
    NotaFiscal SaidaComDuplicatas(int numero, string situacao, int ambiente)
    {
        var origem = CompraComDuplicatas(numero, "Conferida", ambiente);
        return new NotaFiscal
        {
            EmpresaId = empresaId, Tipo = "NFe", Cliente = $"Cliente de teste {empresaId}",
            DocumentoCliente = "12345678901", Numero = origem.NumeroNfe, Serie = origem.Serie,
            ChaveAcesso = origem.ChaveNfe, Situacao = situacao, Ambiente = ambiente, Valor = 999,
            Xml = origem.XmlOriginal.Replace("</ide>", "<natOp>Venda de mercadorias</natOp></ide>")
        };
    }
    var semParcelas = SaidaComDuplicatas(604, "Autorizada", 1);
    semParcelas.Xml = semParcelas.Xml.Replace("dVenc", "dataAusente");
    db.NotasFiscais.AddRange(SaidaComDuplicatas(600, "Autorizada", 1), SaidaComDuplicatas(601, "Cancelada", 1),
        SaidaComDuplicatas(602, "Autorizada", 2), SaidaComDuplicatas(603, "Processando", 1), semParcelas);
    var inicioCaixa = new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);
    var detalheDinheiro = $"<detPag><tPag>01</tPag><vPag>{empresaId}0.50</vPag></detPag>";
    const string detalhePix = "<detPag><tPag>20</tPag><vPag>57.90</vPag></detPag>";
    string ComPagamento(string xml, string detalhes) =>
        xml.Replace("dVenc", "dataAusente").Replace("</infNFe>", $"<pag>{detalhes}<vTroco>3.00</vTroco></pag></infNFe>");
    NotaFiscal SaidaCaixa(int numero, string situacao, int ambiente, DateTime emissao, string detalhes)
    {
        var nota = SaidaComDuplicatas(numero, situacao, ambiente);
        nota.Emissao = emissao;
        nota.Xml = ComPagamento(nota.Xml, detalhes);
        return nota;
    }
    var compraCaixa = CompraComDuplicatas(700, "Conferida", 1);
    compraCaixa.DataEmissao = inicioCaixa.AddHours(2);
    compraCaixa.ValorTotal = empresaId * 200.25m;
    compraCaixa.XmlOriginal = ComPagamento(compraCaixa.XmlOriginal, detalheDinheiro + detalhePix);
    db.EntradasCompra.Add(compraCaixa);
    var compraHomologacao = CompraComDuplicatas(701, "Conferida", 2);
    compraHomologacao.DataEmissao = inicioCaixa;
    compraHomologacao.XmlOriginal = ComPagamento(compraHomologacao.XmlOriginal, detalheDinheiro);
    db.EntradasCompra.Add(compraHomologacao);
    var compraSemConferencia = CompraComDuplicatas(702, "Conferencia", 1);
    compraSemConferencia.DataEmissao = inicioCaixa;
    compraSemConferencia.XmlOriginal = ComPagamento(compraSemConferencia.XmlOriginal, detalheDinheiro);
    db.EntradasCompra.Add(compraSemConferencia);
    db.NotasFiscais.AddRange(
        SaidaCaixa(710, "Autorizada", 1, inicioCaixa, detalheDinheiro + detalheDinheiro + detalhePix),
        SaidaCaixa(711, "Autorizada", 1, inicioCaixa.AddSeconds(-1), detalheDinheiro),
        SaidaCaixa(712, "Autorizada", 1, inicioCaixa, detalhePix),
        SaidaCaixa(713, "Cancelada", 1, inicioCaixa, detalheDinheiro),
        SaidaCaixa(714, "Autorizada", 2, inicioCaixa, detalheDinheiro),
        SaidaCaixa(715, "Autorizada", 1, new DateTime(2019, 1, 15, 2, 30, 0, DateTimeKind.Utc), detalheDinheiro));
    await db.SaveChangesAsync();
}

foreach (var empresaId in new[] { 1, 2 })
{
    client.DefaultRequestHeaders.Remove("Test-Company");
    client.DefaultRequestHeaders.Add("Test-Company", empresaId.ToString());
    const string url = "/api/financeiro/contas-a-pagar";
    var resultado = await client.GetFromJsonAsync<JsonElement>(url);
    Check(resultado.GetProperty("titulos").GetArrayLength() == 2, "Contas a pagar considerou parcelas sem data, de homologação, sem conferência ou de outra empresa.");
    var dezembro = await client.GetFromJsonAsync<JsonElement>($"{url}?de=2026-12-05&ate=2026-12-05");
    var parcelas = dezembro.GetProperty("titulos");
    Check(parcelas.GetArrayLength() == 1 && parcelas[0].GetProperty("parcela").GetString() == "001"
        && parcelas[0].GetProperty("valor").GetDecimal() == empresaId * 100 + 0.50m
        && dezembro.GetProperty("fornecedores")[0].GetProperty("id").GetInt64() == empresaId,
        "Consulta não preservou valor do XML, fornecedor ou vencimento inclusivo.");
    Check((await client.GetAsync($"{url}?empresaId={3 - empresaId}")).StatusCode == HttpStatusCode.Forbidden,
        "Contas a pagar aceitou consultar outra empresa.");
    Check((await client.GetAsync($"{url}?de=2027-01-05&ate=2026-12-05")).StatusCode == HttpStatusCode.BadRequest,
        "Contas a pagar aceitou período invertido.");

    const string receberUrl = "/api/financeiro/contas-a-receber";
    var receber = await client.GetFromJsonAsync<JsonElement>($"{receberUrl}?de=2026-12-05&ate=2026-12-05");
    var aReceber = receber.GetProperty("titulos");
    var cliente = receber.GetProperty("clientes")[0];
    Check(aReceber.GetArrayLength() == 1 && aReceber[0].GetProperty("nota").GetString() == "600"
        && aReceber[0].GetProperty("parcela").GetString() == "001" && aReceber[0].GetProperty("natureza").GetString() == "Venda de mercadorias"
        && aReceber[0].GetProperty("valor").GetDecimal() == empresaId * 100 + 0.50m
        && aReceber[0].GetProperty("clienteId").GetInt64() == cliente.GetProperty("id").GetInt64()
        && cliente.GetProperty("nome").GetString() == $"Cliente de teste {empresaId}",
        "Contas a receber expôs nota inelegível ou perdeu os valores e dados originais da parcela.");
    var todasParcelas = await client.GetFromJsonAsync<JsonElement>(receberUrl);
    Check(todasParcelas.GetProperty("titulos").GetArrayLength() == 2, "Contas a receber inferiu parcelas ou não leu os vencimentos.");
    Check((await client.GetAsync($"{receberUrl}?empresaId={3 - empresaId}")).StatusCode == HttpStatusCode.Forbidden,
        "Contas a receber aceitou consultar outra empresa.");

    const string caixaUrl = "/api/financeiro/caixa/referencia-fiscal";
    var caixa = await client.GetFromJsonAsync<JsonElement>($"{caixaUrl}?de=2026-11-01&ate=2026-11-01");
    var referencia = caixa.GetProperty("documentos");
    Check(referencia.GetArrayLength() == 2 && referencia[0].GetProperty("origem").GetString() == "compra"
        && referencia[0].GetProperty("valor").GetDecimal() == empresaId * 10 + 0.50m
        && referencia[1].GetProperty("origem").GetString() == "saida"
        && referencia[1].GetProperty("valor").GetDecimal() == empresaId * 20 + 1m
        && referencia[1].GetProperty("dia").GetString() == "2026-11-01",
        "Caixa incluiu outro período, empresa, pagamento, situação ou ambiente na referência fiscal.");
    Check((await client.GetAsync($"{caixaUrl}?de=2026-11-01&ate=2026-11-01&empresaId={3 - empresaId}")).StatusCode == HttpStatusCode.Forbidden,
        "Caixa aceitou consultar outra empresa.");
    Check((await client.GetAsync($"{caixaUrl}?de=2025-01-01&ate=2026-11-01")).StatusCode == HttpStatusCode.BadRequest,
        "Caixa aceitou um período maior que 366 dias.");
    var horarioVerao = await client.GetFromJsonAsync<JsonElement>($"{caixaUrl}?de=2019-01-15&ate=2019-01-15");
    Check(horarioVerao.GetProperty("documentos").GetArrayLength() == 1
        && horarioVerao.GetProperty("documentos")[0].GetProperty("dia").GetString() == "2019-01-15",
        "Caixa deslocou um documento emitido durante o horário de verão para o dia anterior.");

    const string movimentosUrl = "/api/financeiro/movimentacoes/referencia-fiscal";
    var fiscal = await client.GetFromJsonAsync<JsonElement>($"{movimentosUrl}?de=2026-11-01&ate=2026-11-01");
    var docsFiscais = fiscal.GetProperty("documentos");
    Check(docsFiscais.GetArrayLength() == 3 && docsFiscais[0].GetProperty("numero").GetString() == "700"
        && docsFiscais[0].GetProperty("valor").GetDecimal() == empresaId * 200.25m
        && docsFiscais[1].GetProperty("valor").GetDecimal() == 999m
        && docsFiscais[2].GetProperty("valor").GetDecimal() == 999m
        && docsFiscais.EnumerateArray().Any(doc => doc.GetProperty("numero").GetString() == "712"),
        "Movimentações não retornou os valores fiscais ou incluiu empresa, período, ambiente ou situação indevida.");
    Check((await client.GetAsync($"{movimentosUrl}?de=2026-11-01&ate=2026-11-01&empresaId={3 - empresaId}")).StatusCode == HttpStatusCode.Forbidden,
        "Movimentações aceitou consultar outra empresa.");
    Check((await client.GetAsync($"{movimentosUrl}?de=2026-11-02&ate=2026-11-01")).StatusCode == HttpStatusCode.BadRequest,
        "Movimentações aceitou período invertido.");
}

Console.WriteLine($"Tenant HTTP checks: {checks} passed. Banco descartavel; nenhum acesso ao HUB ou banco real.");

sealed class ChecksAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static ClaimsPrincipal Principal(int company) => new(new ClaimsIdentity([
        new Claim(TenantContext.EmpresaClaim, company.ToString()), new Claim(HubAuthentication.UsuarioClaim, company.ToString()),
        new Claim(ClaimTypes.NameIdentifier, "user-" + company), new Claim("sub", "user-" + company),
        new Claim("name", "Usuario " + company), new Claim("email", $"usuario{company}@example.test"), new Claim("tenant_id", "tenant-" + company),
    ], "Checks"));
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
        int.TryParse(Request.Headers["Test-Company"], out var id) && id is 1 or 2
            ? AuthenticateResult.Success(new AuthenticationTicket(Principal(id), Scheme.Name)) : AuthenticateResult.NoResult());
}

sealed class ChecksStorage : ICloudflareR2StorageService
{
    public Task<StoredDocument> UploadAsync(long tenantId, string category, IFormFile file, CancellationToken token) => throw new Exception("Upload externo nao deve ocorrer neste teste.");
    public Task<DownloadedDocument?> DownloadAsync(long tenantId, string key, CancellationToken token) => Task.FromResult<DownloadedDocument?>(null);
}
