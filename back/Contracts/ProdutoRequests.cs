namespace back.Contracts;

public record ProdutoResponse(
    int Id,
    int EmpresaId,
    int SKU,
    int Codintern,
    int Codfabricante,
    string Descricao,
    long CategoriaId,
    string Categoria,
    long MarcaId,
    string Marca,
    int FabricanteId,
    double Custo,
    double Preco,
    int EstoqueMinimo,
    int EstoqueMaximo,
    int Ncm,
    int Cest,
    string OrigemMercadoria,
    decimal AliquotaIpi,
    decimal AliquotaIcms,
    decimal AliquotaMva,
    string ImpostosFabricante,
    bool Ativo);

public record ProdutoListaResponse(
    int Pagina,
    int TamanhoPagina,
    int Total,
    int TotalAtivos,
    int TotalInativos,
    IReadOnlyCollection<ProdutoResponse> Produtos);

public record CriarProdutoRequest(
    int EmpresaId,
    int SKU,
    int Codintern,
    int Codfabricante,
    string Descricao,
    long CategoriaId,
    long MarcaId,
    int FabricanteId,
    double Custo,
    double Preco,
    int EstoqueMinimo,
    int EstoqueMaximo,
    int Ncm,
    int Cest,
    string? OrigemMercadoria,
    decimal AliquotaIpi,
    decimal AliquotaIcms,
    decimal AliquotaMva,
    string? ImpostosFabricante,
    bool Ativo);

public record ImportarProdutosFabricanteResponse(
    string Fabricante,
    string Arquivo,
    int LinhasLidas,
    int ProdutosCriados,
    int ProdutosAtualizados,
    int LinhasIgnoradas,
    IReadOnlyCollection<string> Erros,
    IReadOnlyCollection<ProdutoResponse> Produtos);
