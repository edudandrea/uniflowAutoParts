namespace back.Models
{
    public class EntradaCompra
    {
        public long Id { get; set; }
        public long TenantId { get; set; }
        public long? FornecedorId { get; set; }
        public Cliente? Fornecedor { get; set; }
        public string ChaveNfe { get; set; } = string.Empty;
        public string NumeroNfe { get; set; } = string.Empty;
        public string Serie { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public DateTime? DataEmissao { get; set; }
        public DateTime? DataEntrada { get; set; }
        public string TipoOperacao { get; set; } = string.Empty;
        public string Finalidade { get; set; } = string.Empty;
        public string NaturezaOperacao { get; set; } = string.Empty;
        public decimal ValorProdutos { get; set; }
        public decimal ValorFrete { get; set; }
        public decimal ValorSeguro { get; set; }
        public decimal ValorDesconto { get; set; }
        public decimal ValorOutrasDespesas { get; set; }
        public decimal ValorTotal { get; set; }
        public string Status { get; set; } = "Conferencia";
        public string XmlOriginal { get; set; } = string.Empty;
        public DateTime ImportadoEm { get; set; } = DateTime.UtcNow;
        public ICollection<EntradaCompraItem> Itens { get; set; } = [];
    }
}
