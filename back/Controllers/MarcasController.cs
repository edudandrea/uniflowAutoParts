using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/marcas")]
public class MarcasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MarcaResponse>>> Listar(
        [FromQuery] long tenantId,
        [FromQuery] bool incluirInativas = false)
    {
        if (tenantId <= 0)
        {
            return BadRequest("TenantId e obrigatorio.");
        }

        var query = db.Marcas
            .AsNoTracking()
            .Include(marca => marca.Produtos)
            .Where(marca => marca.TenantId == tenantId);

        if (!incluirInativas)
        {
            query = query.Where(marca => marca.Ativo);
        }

        var marcas = await query
            .OrderByDescending(marca => marca.Ativo)
            .ThenBy(marca => marca.Nome)
            .Select(marca => ToResponse(marca))
            .ToListAsync();

        return Ok(marcas);
    }

    [HttpPost]
    public async Task<ActionResult<MarcaResponse>> Criar(CriarMarcaRequest request)
    {
        if (request.TenantId <= 0)
        {
            return BadRequest("TenantId e obrigatorio.");
        }

        var empresaExiste = await db.Empresas.AnyAsync(empresa => empresa.Id == request.TenantId);
        if (!empresaExiste)
        {
            return NotFound("Empresa nao encontrada.");
        }

        var nome = TextoOuNull(request.Nome);
        if (nome is null)
        {
            return BadRequest("Nome e obrigatorio.");
        }

        var codigo = TextoOuNull(request.Codigo);
        var nomeJaExiste = await db.Marcas.AnyAsync(marca =>
            marca.TenantId == request.TenantId &&
            marca.Nome.ToLower() == nome.ToLower());

        if (nomeJaExiste)
        {
            return Conflict("Ja existe uma marca com este nome.");
        }

        if (codigo is not null)
        {
            var codigoJaExiste = await db.Marcas.AnyAsync(marca =>
                marca.TenantId == request.TenantId &&
                marca.Codigo != null &&
                marca.Codigo.ToLower() == codigo.ToLower());

            if (codigoJaExiste)
            {
                return Conflict("Ja existe uma marca com este codigo.");
            }
        }

        var marcaNova = new Marca
        {
            TenantId = request.TenantId,
            Nome = nome,
            Codigo = codigo,
            Descricao = TextoOuNull(request.Descricao),
            LogoUrl = TextoOuNull(request.LogoUrl),
            Site = TextoOuNull(request.Site),
            Observacao = TextoOuNull(request.Observacao),
            Ativo = request.Ativo,
            CriadoEm = DateTime.UtcNow
        };

        db.Marcas.Add(marcaNova);
        await db.SaveChangesAsync();

        return Created($"/api/marcas/{marcaNova.Id}", ToResponse(marcaNova));
    }

    private static MarcaResponse ToResponse(Marca marca)
    {
        return new MarcaResponse(
            marca.Id,
            marca.TenantId,
            marca.Nome,
            marca.Codigo,
            marca.Descricao,
            marca.LogoUrl,
            marca.Site,
            marca.Observacao,
            marca.Ativo,
            marca.CriadoEm,
            marca.AtualizadoEm,
            marca.Produtos.Count);
    }

    private static string? TextoOuNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
