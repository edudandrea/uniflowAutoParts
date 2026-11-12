namespace back.Models;

public sealed class ConfiguracaoFiscal
{
    public int EmpresaId { get; set; }
    public string CodigoMunicipio { get; set; } = "";
    public int SeriePadrao { get; set; } = 1;
    public int Ambiente { get; set; } = 2;
}