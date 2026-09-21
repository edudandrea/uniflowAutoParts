using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/enderecos")]
public class EnderecosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CadastroEnderecoResponse>>> Listar(
        [FromQuery] int empresaId,
        [FromQuery] string? busca)
    {
        if (empresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        var query = db.CadastroEnderecos
            .Where(endereco => endereco.EmpresaId == empresaId);

        var termo = TextoOuNull(busca);
        if (termo is not null)
        {
            var cep = ApenasDigitos(termo);
            query = query.Where(endereco =>
                endereco.Nome.ToLower().Contains(termo.ToLower()) ||
                endereco.Logradouro.ToLower().Contains(termo.ToLower()) ||
                endereco.Bairro.ToLower().Contains(termo.ToLower()) ||
                endereco.Municipio.ToLower().Contains(termo.ToLower()) ||
                (cep != "" && endereco.Cep.Contains(cep)));
        }

        var enderecos = await query
            .OrderByDescending(endereco => endereco.Ativo)
            .ThenBy(endereco => endereco.Logradouro)
            .Select(endereco => ToResponse(endereco))
            .ToListAsync();

        return Ok(enderecos);
    }

    [HttpPost]
    public async Task<ActionResult<CadastroEnderecoResponse>> Criar(CriarCadastroEnderecoRequest request)
    {
        if (request.EmpresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        var empresaExiste = await db.Empresas.AnyAsync(empresa => empresa.Id == request.EmpresaId);
        if (!empresaExiste)
        {
            return NotFound("Empresa nao encontrada.");
        }

        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.Logradouro))
        {
            return BadRequest("Nome e logradouro sao obrigatorios.");
        }

        var cep = ApenasDigitos(request.Cep);
        if (cep.Length != 8)
        {
            return BadRequest("CEP deve conter 8 digitos.");
        }

        var endereco = new CadastroEndereco
        {
            EmpresaId = request.EmpresaId,
            Nome = request.Nome.Trim(),
            Cep = cep,
            Logradouro = request.Logradouro.Trim(),
            Numero = TextoOuNull(request.Numero) ?? "S/N",
            Complemento = TextoOuNull(request.Complemento),
            Bairro = TextoOuNull(request.Bairro) ?? "",
            CodigoIbgeMunicipio = TextoOuNull(request.CodigoIbgeMunicipio) ?? "0000000",
            Municipio = TextoOuNull(request.Municipio) ?? "",
            Uf = request.Uf.Trim().ToUpperInvariant(),
            Pais = TextoOuNull(request.Pais) ?? "Brasil",
            Referencia = TextoOuNull(request.Referencia),
            Ativo = request.Ativo,
            CriadoEm = DateTime.UtcNow
        };

        db.CadastroEnderecos.Add(endereco);
        await db.SaveChangesAsync();

        return Created($"/api/enderecos/{endereco.Id}", ToResponse(endereco));
    }

    private static CadastroEnderecoResponse ToResponse(CadastroEndereco endereco)
    {
        return new CadastroEnderecoResponse(
            endereco.Id,
            endereco.EmpresaId,
            endereco.Nome,
            endereco.Cep,
            endereco.Logradouro,
            endereco.Numero,
            endereco.Complemento,
            endereco.Bairro,
            endereco.CodigoIbgeMunicipio,
            endereco.Municipio,
            endereco.Uf,
            endereco.Pais,
            endereco.Referencia,
            endereco.Ativo);
    }

    private static string ApenasDigitos(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string? TextoOuNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
