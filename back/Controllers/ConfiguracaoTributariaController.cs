using back.Contracts;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/fiscal/tributacao")]
public sealed class ConfiguracaoTributariaController(AppDbContext db, TenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken cancellationToken)
    {
        if (tenant.EmpresaId <= 0) return Unauthorized();
        var settings = await db.ConfiguracoesTributarias.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EmpresaId == tenant.EmpresaId, cancellationToken);
        return Ok(settings is null ? null : ToResponse(settings));
    }

    [HttpPut]
    public async Task<IActionResult> Salvar(ConfiguracaoTributariaRequest request, CancellationToken cancellationToken)
    {
        if (tenant.EmpresaId <= 0) return Unauthorized();
        var settings = await db.ConfiguracoesTributarias
            .SingleOrDefaultAsync(c => c.EmpresaId == tenant.EmpresaId, cancellationToken);
        if (settings is null)
        {
            settings = new ConfiguracaoTributaria { EmpresaId = tenant.EmpresaId };
            db.ConfiguracoesTributarias.Add(settings);
        }
        settings.Regime = request.Regime;
        settings.IcmsCode = request.IcmsCode;
        settings.PisCode = request.PisCode;
        settings.CofinsCode = request.CofinsCode;
        settings.IcmsRate = request.IcmsRate;
        settings.PisRate = request.PisRate;
        settings.CofinsRate = request.CofinsRate;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(settings));
    }

    private static ConfiguracaoTributariaRequest ToResponse(ConfiguracaoTributaria settings) => new(
        settings.Regime, settings.IcmsCode, settings.PisCode, settings.CofinsCode,
        settings.IcmsRate, settings.PisRate, settings.CofinsRate);
}
