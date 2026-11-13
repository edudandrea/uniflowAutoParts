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

        var validation = ValidarCadastro(request);
        if (validation is not null) return BadRequest(validation);

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

        var cliente = new Cliente { TenantId = request.EmpresaId, CriadoEm = DateTime.UtcNow };
        AplicarDados(cliente, request);

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

    [HttpGet("{id:long}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ClienteDetalheResponse>> Obter(long id, [FromQuery] long empresaId)
    {
        var cliente = await db.Clientes.AsNoTracking().Include(c => c.CadastroEndereco).Include(c => c.Enderecos)
            .Include(c => c.Contatos).Include(c => c.DadosComerciais)
            .SingleOrDefaultAsync(c => c.Id == id && c.TenantId == empresaId);
        if (cliente is null) return NotFound("Cliente nao encontrado.");
        var linked = cliente.CadastroEndereco;
        var address = cliente.Enderecos.OrderByDescending(e => e.Principal).ThenBy(e => e.Id).FirstOrDefault();
        CriarClienteEnderecoRequest? endereco = linked is not null
            ? new(TipoEndereco.Comercial, linked.Cep, linked.Logradouro, linked.Numero, linked.Complemento, linked.Bairro, linked.CodigoIbgeMunicipio, linked.Municipio, linked.Uf, linked.Pais, true)
            : address is null ? null : new(address.Tipo, address.Cep, address.Logradouro, address.Numero, address.Complemento, address.Bairro, address.CodigoIbgeMunicipio, address.Municipio, address.Uf, address.Pais, address.Principal);
        var commercial = cliente.DadosComerciais;
        var dados = new CriarClienteRequest(cliente.TenantId, cliente.TipoPessoa, cliente.Relacionamento, cliente.NomeRazaoSocial,
            cliente.NomeFantasia, cliente.CpfCnpj, cliente.Rg, cliente.OrgaoEmissor, cliente.EstadoCivil, cliente.DataNascimento,
            cliente.Profissao, cliente.Cnh, cliente.InscricaoEstadual, cliente.InscricaoMunicipal, cliente.IndicadorIe, cliente.ConsumidorFinal,
            cliente.Email, cliente.Telefone, cliente.Celular, cliente.ClienteDesde, cliente.Origem, cliente.VendedorPadrao, cliente.LimiteCredito,
            cliente.Observacao, cliente.Ativo, cliente.CadastroEnderecoId, endereco,
            cliente.Contatos.OrderByDescending(c => c.Principal).ThenBy(c => c.Id).Select(c => new CriarClienteContatoRequest(c.Nome, c.Cargo, c.Email, c.Telefone, c.Celular, c.Principal)).ToArray(),
            commercial is null ? null : new(commercial.CondicaoPagamento, commercial.LimiteCredito, commercial.TabelaPreco, commercial.VendedorPadrao, commercial.Origem, commercial.BloquearVenda, commercial.PermiteFiado));
        return Ok(new ClienteDetalheResponse(cliente.Id, dados, cliente.CriadoEm, cliente.AtualizadoEm));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, CriarClienteRequest request)
    {
        var cliente = await db.Clientes.Include(c => c.Enderecos).Include(c => c.Contatos).Include(c => c.DadosComerciais)
            .SingleOrDefaultAsync(c => c.Id == id && c.TenantId == request.EmpresaId);
        if (cliente is null) return NotFound("Cliente nao encontrado.");
        var validation = ValidarCadastro(request);
        if (validation is not null) return BadRequest(validation);
        var documento = ApenasDigitos(request.CpfCnpj);
        if (await db.Clientes.AnyAsync(c => c.TenantId == request.EmpresaId && c.Id != id && c.CpfCnpj == documento))
            return Conflict("Ja existe um cliente com este CPF/CNPJ.");
        if (request.EnderecoId is { } addressId && !await db.CadastroEnderecos.AnyAsync(e => e.Id == addressId && e.EmpresaId == request.EmpresaId && (e.Ativo || e.Id == cliente.CadastroEnderecoId)))
            return BadRequest("Endereco selecionado nao foi encontrado para esta empresa.");

        AplicarDados(cliente, request);
        cliente.CadastroEnderecoId = request.EnderecoId;
        if (request.EnderecoId is null && request.Endereco is { } a)
        {
            var address = cliente.Enderecos.OrderByDescending(e => e.Principal).ThenBy(e => e.Id).FirstOrDefault();
            if (address is null) { address = new Endereco(); cliente.Enderecos.Add(address); }
            address.Tipo = a.Tipo; address.Cep = ApenasDigitos(a.Cep); address.Logradouro = a.Logradouro.Trim();
            address.Numero = a.Numero.Trim(); address.Complemento = TextoOuNull(a.Complemento); address.Bairro = a.Bairro.Trim();
            address.CodigoIbgeMunicipio = a.CodigoIbgeMunicipio; address.Municipio = a.Municipio.Trim(); address.Uf = a.Uf.Trim().ToUpperInvariant();
            address.Pais = a.Pais; address.Principal = a.Principal;
        }
        if (request.Contatos is not null)
        {
            var existing = cliente.Contatos.OrderByDescending(c => c.Principal).ThenBy(c => c.Id).ToArray();
            var requested = request.Contatos.Where(c => !string.IsNullOrWhiteSpace(c.Nome)).ToArray();
            for (var i = 0; i < requested.Length; i++)
            {
                var target = i < existing.Length ? existing[i] : new ClienteContato();
                if (i >= existing.Length) cliente.Contatos.Add(target);
                var c = requested[i]; target.Nome = c.Nome.Trim(); target.Cargo = TextoOuNull(c.Cargo); target.Email = TextoOuNull(c.Email);
                target.Telefone = TextoOuNull(c.Telefone); target.Celular = TextoOuNull(c.Celular); target.Principal = c.Principal;
            }
            foreach (var removed in existing.Skip(requested.Length)) db.Remove(removed);
        }
        if (request.DadosComerciais is { } commercial)
        {
            cliente.DadosComerciais ??= new ClienteDadosComerciais();
            var target = cliente.DadosComerciais;
            target.CondicaoPagamento = TextoOuNull(commercial.CondicaoPagamento); target.LimiteCredito = commercial.LimiteCredito;
            target.TabelaPreco = TextoOuNull(commercial.TabelaPreco); target.VendedorPadrao = TextoOuNull(commercial.VendedorPadrao);
            target.Origem = TextoOuNull(commercial.Origem); target.BloquearVenda = commercial.BloquearVenda; target.PermiteFiado = commercial.PermiteFiado;
        }
        cliente.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToResponse(cliente));
    }

    private static string? ValidarCadastro(CriarClienteRequest request)
    {
        if (request.EmpresaId <= 0) return "EmpresaId e obrigatorio.";
        if (string.IsNullOrWhiteSpace(request.NomeRazaoSocial)) return "Nome ou razao social e obrigatorio.";
        var documento = ApenasDigitos(request.CpfCnpj ?? "");
        if (request.TipoPessoa is not (TipoPessoa.fisica or TipoPessoa.juridica)) return "Tipo de pessoa invalido.";
        if (documento.Length != (request.TipoPessoa == TipoPessoa.fisica ? 11 : 14)) return "Informe CPF com 11 digitos ou CNPJ com 14 digitos conforme o tipo de pessoa.";
        if (request.IndicadorIe is not (IndicadorIe.Contribuinte or IndicadorIe.ContribuinteIsento or IndicadorIe.NaoContribuinte)) return "Indicador de IE invalido.";
        if (request.IndicadorIe == IndicadorIe.Contribuinte && string.IsNullOrWhiteSpace(request.InscricaoEstadual)) return "Informe a inscricao estadual do cliente contribuinte.";
        if (request.LimiteCredito < 0 || request.DadosComerciais?.LimiteCredito < 0) return "O limite de credito nao pode ser negativo.";
        return null;
    }

    private static void AplicarDados(Cliente cliente, CriarClienteRequest request)
    {
        cliente.TipoPessoa = request.TipoPessoa;
        cliente.Relacionamento = request.Relacionamento;
        cliente.NomeRazaoSocial = request.NomeRazaoSocial.Trim();
        cliente.NomeFantasia = TextoOuNull(request.NomeFantasia);
        cliente.CpfCnpj = ApenasDigitos(request.CpfCnpj);
        cliente.Rg = TextoOuNull(request.Rg);
        cliente.OrgaoEmissor = TextoOuNull(request.OrgaoEmissor);
        cliente.EstadoCivil = TextoOuNull(request.EstadoCivil);
        cliente.DataNascimento = request.DataNascimento;
        cliente.Profissao = TextoOuNull(request.Profissao);
        cliente.Cnh = TextoOuNull(request.Cnh);
        cliente.InscricaoEstadual = TextoOuNull(request.InscricaoEstadual);
        cliente.InscricaoMunicipal = TextoOuNull(request.InscricaoMunicipal);
        cliente.IndicadorIe = request.IndicadorIe;
        cliente.ConsumidorFinal = request.ConsumidorFinal;
        cliente.Email = TextoOuNull(request.Email);
        cliente.Telefone = TextoOuNull(request.Telefone);
        cliente.Celular = TextoOuNull(request.Celular);
        cliente.ClienteDesde = request.ClienteDesde;
        cliente.Origem = TextoOuNull(request.Origem);
        cliente.VendedorPadrao = TextoOuNull(request.VendedorPadrao);
        cliente.LimiteCredito = request.LimiteCredito;
        cliente.Observacao = TextoOuNull(request.Observacao);
        cliente.Ativo = request.Ativo;
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
