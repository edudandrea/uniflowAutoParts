namespace back.Models
{
    public class ClienteDadosComerciais
    {
        public long Id { get; set; }
        public long ClienteId { get; set; }
        public string? CondicaoPagamento { get; set; }
        public decimal? LimiteCredito { get; set; }
        public string? TabelaPreco { get; set; }
        public string? VendedorPadrao { get; set; }
        public string? Origem { get; set; }
        public bool BloquearVenda { get; set; }
        public bool PermiteFiado { get; set; }

        public Cliente Cliente { get; set; } = null!;
    }
}
