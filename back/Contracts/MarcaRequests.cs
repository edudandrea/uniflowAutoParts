namespace back.Contracts;

public record CriarMarcaRequest(
    long TenantId,
    string Nome,
    string? Codigo,
    string? Descricao,
    string? LogoUrl,
    string? Site,
    string? Observacao,
    bool Ativo);

public record MarcaResponse(
    long Id,
    long TenantId,
    string Nome,
    string? Codigo,
    string? Descricao,
    string? LogoUrl,
    string? Site,
    string? Observacao,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    int Produtos);
