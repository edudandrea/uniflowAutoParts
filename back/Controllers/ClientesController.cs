using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClienteResumoResponse>>> Listar([FromQuery] long empresaId)
    {
        if (empresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        var clientes = await db.Clientes
            .Where(cliente => cliente.TenantId == empresaId)
            .OrderBy(cliente => cliente.NomeRazaoSocial)
            .Select(cliente => new ClienteResumoResponse(
                cliente.Id,
                cliente.TenantId,
                cliente.NomeRazaoSocial,
                cliente.NomeFantasia,
                cliente.CpfCnpj,
                cliente.TipoPessoa,
                cliente.Relacionamento,
                cliente.Email,
                cliente.Telefone,
                cliente.Celular,
                cliente.Ativo,
                cliente.CriadoEm))
            .ToListAsync();

        return Ok(clientes);
    }

    [HttpPost]
    public async Task<ActionResult<ClienteResumoResponse>> Criar(CriarClienteRequest request)
    {
        if (request.EmpresaId <= 0)
        {
            return BadRequest("EmpresaId e obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.NomeRazaoSocial))
        {
            return BadRequest("Nome ou razao social e obrigatorio.");
        }

        var cpfCnpj = ApenasDigitos(request.CpfCnpj);
        if (cpfCnpj.Length is not (11 or 14))
        {
            return BadRequest("CPF/CNPJ deve conter 11 ou 14 digitos.");
        }

        var empresaExiste = await db.Empresas.AnyAsync(empresa => empresa.Id == request.EmpresaId);
        if (!empresaExiste)
        {
            return NotFound("Empresa nao encontrada.");
        }

        var documentoEmUso = await db.Clientes.AnyAsync(cliente =>
            cliente.TenantId == request.EmpresaId && cliente.CpfCnpj == cpfCnpj);

        if (documentoEmUso)
        {
            return Conflict("Ja existe um cliente com este CPF/CNPJ.");
        }

        var cliente = new Cliente
        {
            TenantId = request.EmpresaId,
            TipoPessoa = request.TipoPessoa,
            Relacionamento = request.Relacionamento,
            NomeRazaoSocial = request.NomeRazaoSocial.Trim(),
            NomeFantasia = TextoOuNull(request.NomeFantasia),
            CpfCnpj = cpfCnpj,
            Rg = TextoOuNull(request.Rg),
            OrgaoEmissor = TextoOuNull(request.OrgaoEmissor),
            EstadoCivil = TextoOuNull(request.EstadoCivil),
            DataNascimento = request.DataNascimento,
            Profissao = TextoOuNull(request.Profissao),
            Cnh = TextoOuNull(request.Cnh),
            InscricaoEstadual = TextoOuNull(request.InscricaoEstadual),
            InscricaoMunicipal = TextoOuNull(request.InscricaoMunicipal),
            IndicadorIe = request.IndicadorIe,
            ConsumidorFinal = request.ConsumidorFinal,
            Email = TextoOuNull(request.Email),
            Telefone = TextoOuNull(request.Telefone),
            Celular = TextoOuNull(request.Celular),
            ClienteDesde = request.ClienteDesde,
            Origem = TextoOuNull(request.Origem),
            VendedorPadrao = TextoOuNull(request.VendedorPadrao),
            LimiteCredito = request.LimiteCredito,
            Observacao = TextoOuNull(request.Observacao),
            Ativo = request.Ativo,
            CriadoEm = DateTime.UtcNow
        };

        if (request.EnderecoId is not null)
        {
            var enderecoExiste = await db.CadastroEnderecos.AnyAsync(endereco =>
                endereco.Id == request.EnderecoId &&
                endereco.EmpresaId == request.EmpresaId &&
                endereco.Ativo);

            if (!enderecoExiste)
            {
                return BadRequest("Endereco selecionado nao foi encontrado para esta empresa.");
            }

            cliente.CadastroEnderecoId = request.EnderecoId;
        }

        if (request.Endereco is not null)
        {
            cliente.Enderecos.Add(new Endereco
            {
                Tipo = request.Endereco.Tipo,
                Cep = ApenasDigitos(request.Endereco.Cep),
                Logradouro = request.Endereco.Logradouro.Trim(),
                Numero = request.Endereco.Numero.Trim(),
                Complemento = TextoOuNull(request.Endereco.Complemento),
                Bairro = request.Endereco.Bairro.Trim(),
                CodigoIbgeMunicipio = TextoOuNull(request.Endereco.CodigoIbgeMunicipio) ?? "0000000",
                Municipio = request.Endereco.Municipio.Trim(),
                Uf = request.Endereco.Uf.Trim().ToUpperInvariant(),
                Pais = TextoOuNull(request.Endereco.Pais) ?? "Brasil",
                Principal = request.Endereco.Principal
            });
        }

        foreach (var contato in request.Contatos ?? [])
        {
            if (string.IsNullOrWhiteSpace(contato.Nome))
            {
                continue;
            }

            cliente.Contatos.Add(new ClienteContato
            {
                Nome = contato.Nome.Trim(),
                Cargo = TextoOuNull(contato.Cargo),
                Email = TextoOuNull(contato.Email),
                Telefone = TextoOuNull(contato.Telefone),
                Celular = TextoOuNull(contato.Celular),
                Principal = contato.Principal
            });
        }

        if (request.DadosComerciais is not null)
        {
            cliente.DadosComerciais = new ClienteDadosComerciais
            {
                CondicaoPagamento = TextoOuNull(request.DadosComerciais.CondicaoPagamento),
                LimiteCredito = request.DadosComerciais.LimiteCredito,
                TabelaPreco = TextoOuNull(request.DadosComerciais.TabelaPreco),
                VendedorPadrao = TextoOuNull(request.DadosComerciais.VendedorPadrao),
                Origem = TextoOuNull(request.DadosComerciais.Origem),
                BloquearVenda = request.DadosComerciais.BloquearVenda,
                PermiteFiado = request.DadosComerciais.PermiteFiado
            };
        }

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var response = ToResponse(cliente);
        return Created($"/api/clientes/{cliente.Id}", response);
    }

    private static ClienteResumoResponse ToResponse(Cliente cliente)
    {
        return new ClienteResumoResponse(
            cliente.Id,
            cliente.TenantId,
            cliente.NomeRazaoSocial,
            cliente.NomeFantasia,
            cliente.CpfCnpj,
            cliente.TipoPessoa,
            cliente.Relacionamento,
            cliente.Email,
            cliente.Telefone,
            cliente.Celular,
            cliente.Ativo,
            cliente.CriadoEm);
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
