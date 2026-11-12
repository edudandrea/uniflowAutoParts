using back.Contracts;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/fiscal/ambiente")]
public sealed class AmbienteFiscalController(AppDbContext db, TenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken cancellationToken)
    {
        if (tenant.EmpresaId <= 0) return Unauthorized();
        var ambiente = await db.ConfiguracoesFiscais.AsNoTracking().Where(c => c.EmpresaId == tenant.EmpresaId)
            .Select(c => (int?)c.Ambiente).SingleOrDefaultAsync(cancellationToken) ?? 2;
        var serie = await db.ConfiguracoesFiscais.Where(c => c.EmpresaId == tenant.EmpresaId).Select(c => (int?)c.SeriePadrao).SingleOrDefaultAsync(cancellationToken) ?? 1;
        return Ok(new AmbienteFiscalResponse(SefazAmbientes.Nome(ambiente), SeriePadrao: serie));
    }

    [HttpPut]
    public async Task<IActionResult> Salvar(AmbienteFiscalRequest request, CancellationToken cancellationToken)
    {
        if (tenant.EmpresaId <= 0) return Unauthorized();
        if (request.Environment is not ("homologacao" or "producao")) return BadRequest("Ambiente fiscal invalido.");
        var settings = await db.ConfiguracoesFiscais.SingleOrDefaultAsync(c => c.EmpresaId == tenant.EmpresaId, cancellationToken);
        if (settings is null)
        {
            settings = new ConfiguracaoFiscal { EmpresaId = tenant.EmpresaId };
            db.ConfiguracoesFiscais.Add(settings);
        }
        settings.Ambiente = SefazAmbientes.Codigo(request.Environment);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new AmbienteFiscalResponse(SefazAmbientes.Nome(settings.Ambiente), SeriePadrao: settings.SeriePadrao));
    }
}