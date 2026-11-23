using System.Globalization;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace back.Controllers;

[ApiController]
[Route("api/municipios")]
public sealed class MunicipiosController(AppDbContext db, TenantContext tenant) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, MunicipioResponse> MunicipiosIbge = LerMunicipiosIbge();
    private static readonly IReadOnlyDictionary<string, string> PrefixosPorUf = MunicipiosIbge.Values
        .GroupBy(municipio => municipio.Uf)
        .ToDictionary(grupo => grupo.Key, grupo => grupo.First().CodigoIbge[..2]);

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IReadOnlyList<MunicipioResponse>>> Listar(CancellationToken cancellationToken)
    {
        var manuais = await db.MunicipiosManuais.AsNoTracking()
            .Select(m => new MunicipioResponse(m.CodigoIbge, m.Nome, m.Uf))
            .ToListAsync(cancellationToken);
        return Ok(MunicipiosIbge.Values.Concat(manuais)
            .OrderBy(m => m.Nome, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true))
            .ThenBy(m => m.Uf, StringComparer.Ordinal)
            .ThenBy(m => m.CodigoIbge, StringComparer.Ordinal)
            .ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<MunicipioResponse>> Criar(MunicipioResponse request, CancellationToken cancellationToken)
    {
        var nome = request.Nome?.Trim();
        var uf = request.Uf?.Trim().ToUpperInvariant();
        var codigo = request.CodigoIbge?.Trim();
        if (string.IsNullOrEmpty(nome) || nome.Length > 120)
            return BadRequest("Informe um nome de município com até 120 caracteres.");
        if (uf is null || !PrefixosPorUf.TryGetValue(uf, out var prefixo))
            return BadRequest("Selecione uma unidade federativa válida.");
        if (codigo is null || codigo.Length != 7 || !codigo.All(char.IsAsciiDigit) ||
            !codigo.StartsWith(prefixo, StringComparison.Ordinal))
            return BadRequest("Informe um código IBGE de sete dígitos que corresponda à UF selecionada.");
        if (MunicipiosIbge.ContainsKey(codigo) ||
            await db.MunicipiosManuais.AnyAsync(m => m.CodigoIbge == codigo, cancellationToken))
            return Conflict("Já existe um município com esse código IBGE.");

        db.MunicipiosManuais.Add(new MunicipioManual { EmpresaId = tenant.EmpresaId, CodigoIbge = codigo, Nome = nome, Uf = uf });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict("Já existe um município com esse código IBGE.");
        }

        return Created("/api/municipios", new MunicipioResponse(codigo, nome, uf));
    }

    private static Dictionary<string, MunicipioResponse> LerMunicipiosIbge()
    {
        // Fonte: https://servicodados.ibge.gov.br/api/v1/localidades/municipios?view=nivelado
        using var stream = typeof(MunicipiosController).Assembly.GetManifestResourceStream("municipios-ibge.csv")
            ?? throw new InvalidOperationException("Base de municípios do IBGE não encontrada.");
        using var reader = new StreamReader(stream);
        var municipios = new Dictionary<string, MunicipioResponse>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } linha)
        {
            var campos = linha.Split(';', 3);
            municipios.Add(campos[0], new MunicipioResponse(campos[0], campos[2], campos[1]));
        }
        return municipios;
    }
}

public sealed record MunicipioResponse(string CodigoIbge, string Nome, string Uf);
