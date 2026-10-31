namespace back.Contracts;

public record EstoqueResponse(
    int Id,
    string Codigo,
    string Descricao,
    string Marca,
    string Categoria,
    int Estoque,
    int Minimo,
    double Valor,
    string Fabricante,
    string Ncm);
