namespace back.Contracts
{
    public record CriarAdministradorSaasRequest(
        string Nome,
        string Email,
        string Senha);

    public record LoginRequest(
        string Email,
        string Senha);

    public record BootstrapStatusResponse(
        bool AdministradorSaasCriado);

    public record CriarUsuarioAdministradorRequest(
        string Nome,
        string Email,
        string Senha);

    public record CriarUsuarioEmpresaRequest(
        int EmpresaId,
        string Nome,
        string Email,
        string Senha,
        int Perfil);

    public record CriarEmpresaContratanteRequest(
        string RazaoSocial,
        string NomeFantasia,
        string Cnpj,
        string? Telefone,
        string? Email,
        string? EmailFiscal,
        string? Endereco,
        string? Numero,
        string? Complemento,
        string? Bairro,
        string? Cidade,
        string? Estado,
        string? Cep,
        string? InscricaoMunicipal,
        string? InscricaoEstadual,
        string? LogoUrl,
        bool UtilizaAPAssistant,
        CriarUsuarioAdministradorRequest Administrador);

    public record UsuarioResponse(
        int Id,
        int? EmpresaId,
        string Nome,
        string Email,
        string Perfil,
        bool Ativo,
        DateTime CriadoEm);

    public record EmpresaContratanteResponse(
        int EmpresaId,
        string RazaoSocial,
        string NomeFantasia,
        string Cnpj,
        UsuarioResponse Administrador);

    public record EmpresaResumoResponse(
        int Id,
        string RazaoSocial,
        string NomeFantasia,
        string Cnpj,
        bool Ativo,
        bool AcessoBloqueado,
        DateTime DataCadastro);
}
