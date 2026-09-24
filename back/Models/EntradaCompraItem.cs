namespace back.Models
{
    public class EntradaCompraItem
    {
        public long Id { get; set; }
        public long EntradaCompraId { get; set; }
        public EntradaCompra? EntradaCompra { get; set; }
        public int? ProdutoId { get; set; }
        public CadastroItens? Produto { get; set; }
        public int NumeroItem { get; set; }
        public string CodigoFornecedor { get; set; } = string.Empty;
        public string? Ean { get; set; }
        public string DescricaoXml { get; set; } = string.Empty;
        public string? Ncm { get; set; }
        public string? Cest { get; set; }
        public string? Cfop { get; set; }
        public string UnidadeComercial { get; set; } = string.Empty;
        public decimal QuantidadeComercial { get; set; }
        public decimal ValorUnitarioComercial { get; set; }
        public string UnidadeTributavel { get; set; } = string.Empty;
        public decimal QuantidadeTributavel { get; set; }
        public decimal ValorUnitarioTributavel { get; set; }
        public decimal ValorProduto { get; set; }
        public decimal ValorFrete { get; set; }
        public decimal ValorSeguro { get; set; }
        public decimal ValorDesconto { get; set; }
        public decimal ValorOutrasDespesas { get; set; }
        public decimal QuantidadeRecebida { get; set; }
        public decimal CustoUnitario { get; set; }
        public string? PedidoCompra { get; set; }
        public string? ItemPedido { get; set; }
        public string? TributacaoXml { get; set; }
    }
}
