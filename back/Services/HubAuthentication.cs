using System.Globalization;
using System.Security.Claims;
using back.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace back.Services;

public sealed class HubAuthSettings
{
    public bool Enabled { get; }
    public Uri? Authority { get; }
    public Uri? PublicOrigin { get; }
    public int ProjectId { get; }
    public bool Development { get; }
    public string ClientId => $"uniflow-project-{ProjectId}";
    public string? PortalUrl => Authority is null ? null : new Uri(Authority, "/?aplicacoes=1").AbsoluteUri;

    public HubAuthSettings(IConfiguration config, IHostEnvironment environment)
    {
        Development = environment.IsDevelopment();
        Enabled = config.GetValue<bool>("HubAuth:Enabled");
        if (!Enabled) return;
        ProjectId = config.GetValue<int>("HubAuth:ProjectId");
        if (!Uri.TryCreate(config["HubAuth:Authority"], UriKind.Absolute, out var authority) ||
            !Uri.TryCreate(config["HubAuth:PublicOrigin"], UriKind.Absolute, out var origin) || ProjectId <= 1 ||
            authority.AbsoluteUri.Length > 500 || !string.IsNullOrEmpty(authority.Query) || !string.IsNullOrEmpty(authority.Fragment) || !string.IsNullOrEmpty(authority.UserInfo) ||
            !Allowed(authority) || !Allowed(origin) || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment) || !string.IsNullOrEmpty(origin.UserInfo))
            throw new InvalidOperationException("Configure HubAuth:Authority, ProjectId (>1) e PublicOrigin com os enderecos HTTPS reais do HUB e do frontend.");
        Authority = authority;
        PublicOrigin = origin;

        bool Allowed(Uri uri) => uri.Scheme == "https" || (Development && uri.IsLoopback && uri.Scheme == "http");
    }
}

public static class HubAuthentication
{
    public const string CookieScheme = "AutoPartsSession";
    public const string OidcScheme = "UniFlowHub";
    public const string UsuarioClaim = "autoparts_usuario_id";

    public static void AddHubAuthentication(this WebApplicationBuilder builder, string connectionString)
    {
        var config = new HubAuthSettings(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(config);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<TenantContext>();
        builder.Services.AddScoped<TenantActionFilter>();
        builder.Services.AddScoped<HubIdentityService>();
        builder.Services.AddDbContext<HubDirectoryDbContext>(options => options.UseNpgsql(connectionString));
        var protection = builder.Services.AddDataProtection().SetApplicationName("UniflowAutoParts");
        var keysPath = builder.Configuration["HubAuth:DataProtectionPath"];
        if (config.Enabled && !config.Development && string.IsNullOrWhiteSpace(keysPath))
            throw new InvalidOperationException("Configure HubAuth:DataProtectionPath em um volume persistente para as sessoes do AutoParts.");
        if (!string.IsNullOrWhiteSpace(keysPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        var auth = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieScheme;
            options.DefaultChallengeScheme = CookieScheme;
        }).AddCookie(CookieScheme, options =>
        {
            options.Cookie.Name = config.Development ? "UniflowParts.Session" : "__Host-UniflowParts.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = config.Development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
            options.Events.OnValidatePrincipal = async ctx =>
            {
                var directory = ctx.HttpContext.RequestServices.GetRequiredService<HubDirectoryDbContext>();
                var companyId = ctx.Principal?.FindFirstValue(TenantContext.EmpresaClaim);
                var userId = ctx.Principal?.FindFirstValue(UsuarioClaim);
                var subject = ctx.Principal?.FindFirstValue("sub");
                var hubTenant = ctx.Principal?.FindFirstValue("tenant_id");
                var valid = config.Enabled && ctx.Principal is not null && ValidProjectClaims(ctx.Principal, config.ProjectId) && int.TryParse(companyId, out var company) && int.TryParse(userId, out var user) &&
                    await directory.Users.AsNoTracking().AnyAsync(u => u.Id == user && u.EmpresaId == company && u.HubSubject == subject && u.Ativo &&
                        u.Empresa != null && u.Empresa.Ativo && !u.Empresa.AcessoBloqueado &&
                        u.Empresa.HubTenantId == hubTenant && u.Empresa.HubIssuer == config.Authority!.AbsoluteUri, ctx.HttpContext.RequestAborted);
                if (!valid) { ctx.RejectPrincipal(); await ctx.HttpContext.SignOutAsync(CookieScheme); }
            };
        });
        if (config.Enabled)
        {
            auth.AddOpenIdConnect(OidcScheme, options =>
            {
                options.SignInScheme = CookieScheme;
                options.Authority = config.Authority!.AbsoluteUri;
                options.ClientId = config.ClientId;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.RequireHttpsMetadata = !config.Development;
                options.CallbackPath = "/api/auth/callback";
                options.MapInboundClaims = false;
                options.SaveTokens = false;
                options.Scope.Clear(); options.Scope.Add("openid"); options.Scope.Add("profile"); options.Scope.Add("email");
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidAudience = config.ClientId;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
                options.CorrelationCookie.SecurePolicy = options.NonceCookie.SecurePolicy = config.Development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.CorrelationCookie.SameSite = options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.Events.OnTicketReceived = async ctx =>
                {
                    if (!ValidProjectClaims(ctx.Principal!, config.ProjectId)) { ctx.Fail("Identidade nao autorizada para este projeto."); return; }
                    try
                    {
                        var directory = ctx.HttpContext.RequestServices.GetRequiredService<HubIdentityService>();
                        var identity = await directory.VincularAsync(ctx.Principal!, config.Authority.AbsoluteUri, ctx.HttpContext.RequestAborted);
                        var claims = (ClaimsIdentity)ctx.Principal!.Identity!;
                        foreach (var claim in claims.Claims.Where(c => (c.Type.StartsWith("autoparts_", StringComparison.Ordinal) || c.Type == ClaimTypes.NameIdentifier)).ToArray()) claims.RemoveClaim(claim);
                        claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, $"{identity.Empresa.Id}:{identity.Usuario.Id}"));
                        claims.AddClaim(new Claim(TenantContext.EmpresaClaim, identity.Empresa.Id.ToString(CultureInfo.InvariantCulture)));
                        claims.AddClaim(new Claim(UsuarioClaim, identity.Usuario.Id.ToString(CultureInfo.InvariantCulture)));
                        ctx.Properties!.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20);
                        ctx.Properties.IsPersistent = false;
                    }
                    catch (Exception error) when (error is InvalidOperationException or DbUpdateException)
                    {
                        ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HubLogin")
                            .LogWarning("Nao foi possivel vincular a identidade do HUB. Verifique os claims e os vinculos de empresa. Tipo: {Tipo}", error.GetType().Name);
                        ctx.Fail("Nao foi possivel vincular a empresa e o usuario do HUB.");
                    }
                };
                options.Events.OnRemoteFailure = ctx =>
                {
                    ctx.HandleResponse();
                    ctx.Response.Redirect("/entrar?erro=hub");
                    return Task.CompletedTask;
                };
            });
        }
        builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder(CookieScheme).RequireAuthenticatedUser().Build());
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-Parts-CSRF";
            options.Cookie.Name = config.Development ? "UniflowParts.Csrf" : "__Host-UniflowParts.Csrf";
            options.Cookie.SecurePolicy = config.Development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
    }

    public static bool ValidProjectClaims(ClaimsPrincipal principal, int projectId) =>
        principal.FindAll("project_id").Count() == 1 && principal.FindFirstValue("project_id") == projectId.ToString(CultureInfo.InvariantCulture) &&
        principal.FindAll("tenant_id").Count() == 1 && !string.IsNullOrWhiteSpace(principal.FindFirstValue("tenant_id")) &&
        principal.FindAll("sub").Count() == 1 && !string.IsNullOrWhiteSpace(principal.FindFirstValue("sub"));
}
