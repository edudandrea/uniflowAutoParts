namespace back.Models;

public sealed class MunicipioManual
{
    public int EmpresaId { get; set; }
    public required string CodigoIbge { get; set; }
    public required string Nome { get; set; }
    public required string Uf { get; set; }
}
