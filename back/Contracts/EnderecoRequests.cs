namespace back.Contracts;

public record CriarCadastroEnderecoRequest(
    int EmpresaId,
    string Nome,
    string Cep,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string? CodigoIbgeMunicipio,
    string Municipio,
    string Uf,
    string? Pais,
    string? Referencia,
    bool Ativo);

public record CadastroEnderecoResponse(
    long Id,
    int EmpresaId,
    string Nome,
    string Cep,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string CodigoIbgeMunicipio,
    string Municipio,
    string Uf,
    string Pais,
    string? Referencia,
    bool Ativo);
