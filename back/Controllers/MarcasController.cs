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

    [HttpPut("{id:long}")]
    public async Task<ActionResult<MarcaResponse>> Atualizar(long id, AtualizarMarcaRequest request)
    {
        if (id <= 0)
        {
            return BadRequest("Id e obrigatorio.");
        }

        if (request.TenantId <= 0)
        {
            return BadRequest("TenantId e obrigatorio.");
        }

        var marca = await db.Marcas
            .Include(marca => marca.Produtos)
            .FirstOrDefaultAsync(marca => marca.Id == id && marca.TenantId == request.TenantId);

        if (marca is null)
        {
            return NotFound("Marca nao encontrada.");
        }

        var nome = TextoOuNull(request.Nome);
        if (nome is null)
        {
            return BadRequest("Nome e obrigatorio.");
        }

        var codigo = TextoOuNull(request.Codigo);
        var nomeJaExiste = await db.Marcas.AnyAsync(outraMarca =>
            outraMarca.Id != id &&
            outraMarca.TenantId == request.TenantId &&
            outraMarca.Nome.ToLower() == nome.ToLower());

        if (nomeJaExiste)
        {
            return Conflict("Ja existe uma marca com este nome.");
        }

        if (codigo is not null)
        {
            var codigoJaExiste = await db.Marcas.AnyAsync(outraMarca =>
                outraMarca.Id != id &&
                outraMarca.TenantId == request.TenantId &&
                outraMarca.Codigo != null &&
                outraMarca.Codigo.ToLower() == codigo.ToLower());

            if (codigoJaExiste)
            {
                return Conflict("Ja existe uma marca com este codigo.");
            }
        }

        marca.Nome = nome;
        marca.Codigo = codigo;
        marca.Descricao = TextoOuNull(request.Descricao);
        marca.LogoUrl = TextoOuNull(request.LogoUrl);
        marca.Site = TextoOuNull(request.Site);
        marca.Observacao = TextoOuNull(request.Observacao);
        marca.Ativo = request.Ativo;
        marca.AtualizadoEm = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(ToResponse(marca));
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
