namespace back.Contracts;

public record CriarCategoriaRequest(
    long TenantId,
    string Nome,
    string? Descricao,
    long? CategoriaPaiId,
    string? Icone,
    int Ordem,
    bool Ativo);

public record CategoriaResponse(
    long Id,
    long TenantId,
    string Nome,
    string? Descricao,
    long? CategoriaPaiId,
    string? CategoriaPaiNome,
    string? Icone,
    int Ordem,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    int Subcategorias,
    int Produtos);
