namespace back.Models;

public sealed class ConfiguracaoTributaria
{
    public int EmpresaId { get; set; }
    public string Regime { get; set; } = string.Empty;
    public string IcmsCode { get; set; } = string.Empty;
    public string PisCode { get; set; } = string.Empty;
    public string CofinsCode { get; set; } = string.Empty;
    public decimal? IcmsRate { get; set; }
    public decimal? PisRate { get; set; }
    public decimal? CofinsRate { get; set; }
}
