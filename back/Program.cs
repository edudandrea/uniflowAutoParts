
using back.Data;
using back.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers e OpenAPI
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<TenantActionFilter>();
    options.Filters.Add<CsrfValidationFilter>();
});
builder.Services.AddOpenApi();
builder.Services.AddScoped<SefazEventosService>();

// Cloudflare R2
builder.Services.Configure<CloudflareR2Options>(
    builder.Configuration.GetSection("CloudflareR2"));

builder.Services.AddSingleton<
    ICloudflareR2StorageService,
    CloudflareR2StorageService>();

// PostgreSQL - conexão conforme ambiente
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings__DefaultConnection no serviço da API antes de iniciar. " +
        "A conexão deve apontar para um servidor PostgreSQL existente.");
}

builder.AddHubAuthentication(connectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, postgres => postgres.EnableRetryOnFailure()));

// CORS - origens configuradas por ambiente
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Cria o banco, se necessário, e aplica apenas as migrations pendentes.
// A API só aceita requisições depois que o esquema estiver atualizado.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        app.Logger.LogInformation("Preparando o banco PostgreSQL e aplicando migrations pendentes.");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Banco PostgreSQL atualizado. API pronta para iniciar.");
    }
    catch (Exception exception)
    {
        app.Logger.LogCritical(exception,
            "Falha ao preparar o banco PostgreSQL. A inicialização da API será interrompida.");
        throw;
    }
}

// OpenAPI somente em desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No Railway, o HTTPS termina no proxy.
// Não forçamos redirecionamento dentro do container.
// Em outras hospedagens, configure HTTPS conforme
// o proxy e os forwarded headers utilizados.

app.UseCors("FrontendPolicy");

// A origem publica configurada evita confiar em headers de proxy enviados pelo cliente.
var hub = app.Services.GetRequiredService<HubAuthSettings>();
app.Use(async (context, next) =>
{
    if (hub.PublicOrigin is { } origin)
    {
        context.Request.Scheme = origin.Scheme;
        context.Request.Host = HostString.FromUriComponent(origin);
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
