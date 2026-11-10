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
await HubRedirectChecks.RunAsync();
await NfeImportChecks.RunAsync();
TenantContext Tenant(int id) => new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(id) } });

var directoryOptions = new DbContextOptionsBuilder<HubDirectoryDbContext>().UseInMemoryDatabase("tenant-checks", root).Options;
await using (var directory = new HubDirectoryDbContext(directoryOptions))
{
    directory.Empresas.AddRange(new Empresas { Id = 1, RazaoSocial = "EMPRESA A", NomeFantasia = "EMPRESA A", Cnpj = "11111111000111" },
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
    db.CadastroItens.Add(new CadastroItens { Id = tenantId, EmpresaId = tenantId, SKU = tenantId, Descricao = $"Produto {tenantId}", MarcaId = tenantId, CategoriaId = tenantId, Ativo = true });
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
    Check(stock.GetArrayLength() == 1 && stock[0].GetProperty("estoque").GetInt32() == tenantId * 5, "Saldo de estoque vazou entre empresas.");
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
