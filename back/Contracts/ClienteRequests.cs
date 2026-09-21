using back.Models;

namespace back.Contracts;

public record CriarClienteRequest(
    long EmpresaId,
    TipoPessoa TipoPessoa,
    RelacionamentoCadastro Relacionamento,
    string NomeRazaoSocial,
    string? NomeFantasia,
    string CpfCnpj,
    string? Rg,
    string? OrgaoEmissor,
    string? EstadoCivil,
    DateOnly? DataNascimento,
    string? Profissao,
    string? Cnh,
    string? InscricaoEstadual,
    string? InscricaoMunicipal,
    IndicadorIe IndicadorIe,
    bool ConsumidorFinal,
    string? Email,
    string? Telefone,
    string? Celular,
    DateOnly? ClienteDesde,
    string? Origem,
    string? VendedorPadrao,
    decimal? LimiteCredito,
    string? Observacao,
    bool Ativo,
    long? EnderecoId,
    CriarClienteEnderecoRequest? Endereco,
    IReadOnlyCollection<CriarClienteContatoRequest>? Contatos,
    CriarClienteDadosComerciaisRequest? DadosComerciais);

public record CriarClienteEnderecoRequest(
    TipoEndereco Tipo,
    string Cep,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string CodigoIbgeMunicipio,
    string Municipio,
    string Uf,
    string Pais,
    bool Principal);

public record CriarClienteContatoRequest(
    string Nome,
    string? Cargo,
    string? Email,
    string? Telefone,
    string? Celular,
    bool Principal);

public record CriarClienteDadosComerciaisRequest(
    string? CondicaoPagamento,
    decimal? LimiteCredito,
    string? TabelaPreco,
    string? VendedorPadrao,
    string? Origem,
    bool BloquearVenda,
    bool PermiteFiado);

public record ClienteResumoResponse(
    long Id,
    long EmpresaId,
    string NomeRazaoSocial,
    string? NomeFantasia,
    string CpfCnpj,
    TipoPessoa TipoPessoa,
    RelacionamentoCadastro Relacionamento,
    string? Email,
    string? Telefone,
    string? Celular,
    bool Ativo,
    DateTime CriadoEm);
