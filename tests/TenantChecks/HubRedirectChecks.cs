using back.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

internal static class HubRedirectChecks
{
    public static async Task RunAsync()
    {
        var cases = new[] {
            (Origin: "https://parts.uniflowtech.com.br/", Callback: "https://parts.uniflowtech.com.br/api/auth/callback"),
            (Origin: "https://parts.uniflowtech.com.br:443/", Callback: "https://parts.uniflowtech.com.br/api/auth/callback"),
            (Origin: "https://localhost:7443/", Callback: "https://localhost:7443/api/auth/callback"),
            (Origin: "http://localhost:8080/", Callback: "http://localhost:8080/api/auth/callback")
        };
        foreach (var test in cases)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["HubAuth:Enabled"] = "true", ["HubAuth:Authority"] = "https://auth.example.test/",
                ["HubAuth:PublicOrigin"] = test.Origin, ["HubAuth:ProjectId"] = "3"
            });
            builder.AddHubAuthentication("Host=localhost;Database=unused_redirect_checks;Username=unused");
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            // Static metadata avoids any call to the real HUB or database.
            builder.Services.PostConfigure<OpenIdConnectOptions>(HubAuthentication.OidcScheme, options =>
                options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration {
                    Issuer = "https://auth.example.test/", AuthorizationEndpoint = "https://auth.example.test/connect/authorize",
                    TokenEndpoint = "https://auth.example.test/connect/token"
                }));
            await using var app = builder.Build();
            app.Use(async (context, next) => {
                HubAuthentication.ApplyPublicOrigin(context.Request, new Uri(test.Origin));
                await next();
            });
            app.UseAuthentication(); app.UseAuthorization();
            app.MapGet("/start", (Microsoft.AspNetCore.Http.HttpContext context) =>
                context.ChallengeAsync(HubAuthentication.OidcScheme, new AuthenticationProperties { RedirectUri = "/app/dashboard" })).AllowAnonymous();
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-Host", "attacker.example.test");
            var response = await client.GetAsync("/start");
            if (response.StatusCode != System.Net.HttpStatusCode.Redirect || response.Headers.Location is not Uri target)
                throw new Exception("OIDC challenge failed: " + await response.Content.ReadAsStringAsync());
            var query = QueryHelpers.ParseQuery(target.Query);
            if (query["redirect_uri"] != test.Callback || query["client_id"] != "uniflow-project-3" || query["code_challenge_method"] != "S256")
                throw new Exception("Incorrect OIDC callback for " + test.Origin + ": " + query["redirect_uri"]);
        }
        Console.WriteLine("Hub redirect checks: 4 passed; canonical HTTPS callback, explicit default port, custom ports and proxy host isolation.");
    }
}
