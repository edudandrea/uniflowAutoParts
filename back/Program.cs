
using back.Data;
using back.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers e OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Cloudflare R2
builder.Services.Configure<CloudflareR2Options>(
    builder.Configuration.GetSection("CloudflareR2"));

builder.Services.AddSingleton<
    ICloudflareR2StorageService,
    CloudflareR2StorageService>();

// PostgreSQL - conexão conforme ambiente
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string DefaultConnection não configurada.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

app.MapControllers();

app.Run();
