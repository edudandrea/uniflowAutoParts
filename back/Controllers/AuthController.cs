using System.Security.Claims;
using back.Data;
using back.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, HubAuthSettings hub, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("session"), AllowAnonymous]
    public async Task<IActionResult> Session(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        if (User.Identity?.IsAuthenticated != true)
            return Ok(new { authenticated = false, available = hub.Enabled, portalUrl = hub.PortalUrl });
        var company = await db.Empresas.AsNoTracking().Select(e => new
        {
            e.Id, nome = e.NomeFantasia == "" ? e.RazaoSocial : e.NomeFantasia,
            e.RazaoSocial, e.Cnpj, e.HubTenantId
        }).SingleOrDefaultAsync(token);
        if (company is null) return Unauthorized();
        return Ok(new
        {
            authenticated = true, available = hub.Enabled, empresa = company,
            usuario = new
            {
                id = User.FindFirstValue(HubAuthentication.UsuarioClaim), sujeito = User.FindFirstValue("sub"),
                nome = User.FindFirstValue("name"), email = User.FindFirstValue("email"), perfil = User.FindFirstValue("role")
            },
            csrfToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken, portalUrl = hub.PortalUrl
        });
    }

    [HttpGet("login"), AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnTo)
    {
        if (!hub.Enabled) return Problem(statusCode: 503, detail: "A integracao com o HUB ainda nao foi configurada no servidor.");
        var destination = returnTo is { Length: < 1500 } && Url.IsLocalUrl(returnTo) && returnTo.StartsWith("/app/", StringComparison.Ordinal)
            ? returnTo : "/app/dashboard";
        return Challenge(new AuthenticationProperties { RedirectUri = destination }, HubAuthentication.OidcScheme);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(HubAuthentication.CookieScheme);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { portalUrl = hub.PortalUrl });
    }
}
