using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CategoriaResponse>>> Listar(
        [FromQuery] long tenantId,
        [FromQuery] bool incluirInativas = false)
    {
        if (tenantId <= 0)
        {
            return BadRequest("TenantId e obrigatorio.");
        }

        var query = db.Categorias
            .AsNoTracking()
            .Include(categoria => categoria.CategoriaPai)
            .Include(categoria => categoria.Subcategorias)
            .Include(categoria => categoria.Produtos)
            .Where(categoria => categoria.TenantId == tenantId);

        if (!incluirInativas)
        {
            query = query.Where(categoria => categoria.Ativo);
        }

        var categorias = await query
            .OrderBy(categoria => categoria.CategoriaPaiId == null ? 0 : 1)
            .ThenBy(categoria => categoria.Ordem)
            .ThenBy(categoria => categoria.Nome)
            .Select(categoria => ToResponse(categoria))
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaResponse>> Criar(CriarCategoriaRequest request)
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

        Categoria? categoriaPai = null;
        if (request.CategoriaPaiId is not null)
        {
            categoriaPai = await db.Categorias.FirstOrDefaultAsync(categoria =>
                categoria.Id == request.CategoriaPaiId &&
                categoria.TenantId == request.TenantId);

            if (categoriaPai is null)
            {
                return BadRequest("Categoria superior nao foi encontrada para esta empresa.");
            }
        }

        var nomeJaExiste = await db.Categorias.AnyAsync(categoria =>
            categoria.TenantId == request.TenantId &&
            categoria.CategoriaPaiId == request.CategoriaPaiId &&
            categoria.Nome.ToLower() == nome.ToLower());

        if (nomeJaExiste)
        {
            return Conflict("Ja existe uma categoria com este nome no mesmo nivel.");
        }

        var categoriaNova = new Categoria
        {
            TenantId = request.TenantId,
            Nome = nome,
            Descricao = TextoOuNull(request.Descricao),
            CategoriaPaiId = request.CategoriaPaiId,
            CategoriaPai = categoriaPai,
            Icone = TextoOuNull(request.Icone),
            Ordem = request.Ordem,
            Ativo = request.Ativo,
            CriadoEm = DateTime.UtcNow
        };

        db.Categorias.Add(categoriaNova);
        await db.SaveChangesAsync();

        categoriaNova.CategoriaPai = categoriaPai;
        return Created($"/api/categorias/{categoriaNova.Id}", ToResponse(categoriaNova));
    }

    private static CategoriaResponse ToResponse(Categoria categoria)
    {
        return new CategoriaResponse(
            categoria.Id,
            categoria.TenantId,
            categoria.Nome,
            categoria.Descricao,
            categoria.CategoriaPaiId,
            categoria.CategoriaPai?.Nome,
            categoria.Icone,
            categoria.Ordem,
            categoria.Ativo,
            categoria.CriadoEm,
            categoria.AtualizadoEm,
            categoria.Subcategorias.Count,
            categoria.Produtos.Count);
    }

    private static string? TextoOuNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
