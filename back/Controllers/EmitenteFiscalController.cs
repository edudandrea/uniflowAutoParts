using back.Contracts;
using back.Data;
using back.Models;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/fiscal/emitente")]
public sealed class EmitenteFiscalController(AppDbContext db, TenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken token)
    {
        var empresa = await db.Empresas.SingleOrDefaultAsync(e => e.Id == tenant.EmpresaId, token);
        if (empresa is null) return NotFound();
        var config = await db.ConfiguracoesFiscais.SingleOrDefaultAsync(c => c.EmpresaId == tenant.EmpresaId, token) ?? new ConfiguracaoFiscal();
        var next = await db.NumeracoesNfe.Where(n => n.EmpresaId == tenant.EmpresaId && n.Ambiente == config.Ambiente && n.Serie == config.SeriePadrao).Select(n => (int?)n.ProximoNumero).SingleOrDefaultAsync(token)
            ?? await ProximoLivre(db, tenant.EmpresaId, config.Ambiente, config.SeriePadrao, token);
        return Ok(new EmitenteFiscalResponse(empresa.Cnpj ?? "", new(empresa.RazaoSocial, empresa.InscricaoEstadual ?? "", empresa.Endereco ?? "", empresa.Numero ?? "", empresa.Complemento, empresa.Bairro ?? "", empresa.Cidade ?? "", config.CodigoMunicipio, empresa.Estado ?? "RS", empresa.Cep ?? "", config.SeriePadrao, next)));
    }

    internal static async Task<int> ProximoLivre(AppDbContext db, int empresa, int ambiente, int serie, CancellationToken token)
    {
        var numeros = await db.NotasFiscais.Where(n => n.EmpresaId == empresa && n.Ambiente == ambiente && n.Serie == serie.ToString() && n.Tipo == "NFe").Select(n => n.Numero).ToListAsync(token);
        return checked(numeros.Select(n => int.TryParse(n, out var v) ? v : 0).DefaultIfEmpty(0).Max() + 1);
    }

    [HttpGet("~/api/fiscal/destinatario/{id:long}")]
    public async Task<IActionResult> Destinatario(long id, CancellationToken token)
    {
        var cliente = await db.Clientes.AsNoTracking().Include(c => c.CadastroEndereco).Include(c => c.Enderecos)
            .SingleOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.EmpresaId, token);
        if (cliente is null) return NotFound();
        var cadastro = cliente.CadastroEndereco;
        var endereco = cliente.Enderecos.OrderByDescending(e => e.Principal).FirstOrDefault();
        return Ok(new DestinatarioFiscalRequest(cliente.InscricaoEstadual ?? "", (int)cliente.IndicadorIe, cliente.ConsumidorFinal,
            cadastro?.Logradouro ?? endereco?.Logradouro ?? "", cadastro?.Numero ?? endereco?.Numero ?? "", cadastro?.Complemento ?? endereco?.Complemento,
            cadastro?.Bairro ?? endereco?.Bairro ?? "", cadastro?.Municipio ?? endereco?.Municipio ?? "", cadastro?.CodigoIbgeMunicipio ?? endereco?.CodigoIbgeMunicipio ?? "",
            cadastro?.Uf ?? endereco?.Uf ?? "RS", cadastro?.Cep ?? endereco?.Cep ?? ""));
    }
    [HttpPut]
    public async Task<IActionResult> Salvar(EmitenteFiscalRequest request, CancellationToken token)
    {
        if (tenant.EmpresaId <= 0) return Unauthorized();
        if (request.Serie is < 0 or > 889 || request.ProximoNumero is < 1 or > 999999999) return BadRequest("Informe serie de 0 a 889 e proximo numero de 1 a 999999999.");
        await using var gate = await FiscalLock.AdquirirAsync(db, tenant.EmpresaId, token);
        if (gate is null) return Conflict("Existe uma operacao fiscal em andamento. Tente novamente.");
        var empresa = await db.Empresas.SingleAsync(e => e.Id == tenant.EmpresaId, token);
        var config = await db.ConfiguracoesFiscais.SingleOrDefaultAsync(c => c.EmpresaId == tenant.EmpresaId, token);
        if (config is null) { config = new ConfiguracaoFiscal { EmpresaId = tenant.EmpresaId }; db.ConfiguracoesFiscais.Add(config); }
        var numeracao = await db.NumeracoesNfe.SingleOrDefaultAsync(n => n.EmpresaId == tenant.EmpresaId && n.Ambiente == config.Ambiente && n.Serie == request.Serie, token);
        var minimo = Math.Max(numeracao?.ProximoNumero ?? 1, await ProximoLivre(db, tenant.EmpresaId, config.Ambiente, request.Serie, token));
        if (request.ProximoNumero < minimo) return BadRequest($"A numeracao nao pode retroceder. O proximo numero disponivel e {minimo}.");
        if (request.Cnpj is not null)
        {
            var cnpj = NfeGerador.Digitos(request.Cnpj);
            if (cnpj.Length != 14) return BadRequest("Informe o CNPJ do emitente com 14 digitos.");
            empresa.Cnpj = cnpj;
        }
        empresa.RazaoSocial = request.RazaoSocial?.Trim() ?? ""; empresa.InscricaoEstadual = NfeGerador.Digitos(request.InscricaoEstadual);
        empresa.Endereco = request.Logradouro?.Trim(); empresa.Numero = request.Numero?.Trim(); empresa.Complemento = request.Complemento?.Trim();
        empresa.Bairro = request.Bairro?.Trim(); empresa.Cidade = request.Municipio?.Trim(); empresa.Estado = request.Uf?.Trim().ToUpperInvariant(); empresa.Cep = NfeGerador.Digitos(request.Cep);
        config.CodigoMunicipio = request.CodigoMunicipio?.Trim() ?? ""; config.SeriePadrao = request.Serie;
        try { NfeGerador.ValidarEmitente(empresa, config); } catch (ArgumentException e) { return BadRequest(e.Message); }
        if (numeracao is null) { numeracao = new NumeracaoNfe { EmpresaId = tenant.EmpresaId, Ambiente = config.Ambiente, Serie = request.Serie }; db.NumeracoesNfe.Add(numeracao); }
        numeracao.ProximoNumero = request.ProximoNumero;
        await db.SaveChangesAsync(token);
        return await Obter(token);
    }
}
