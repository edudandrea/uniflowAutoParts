namespace back.Models;

// Documentos de saida. Entradas de compra permanecem em EntradaCompra.
public class NotaFiscal
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public string Numero { get; set; } = "";
    public string Serie { get; set; } = "";
    public DateTime Emissao { get; set; }
    public string Cliente { get; set; } = "";
    public string DocumentoCliente { get; set; } = "";
    public string Tipo { get; set; } = "NFe";
    public string Situacao { get; set; } = "Autorizada";
    public decimal Valor { get; set; }
    public string ChaveAcesso { get; set; } = "";
    public string Protocolo { get; set; } = "";
    public int Ambiente { get; set; }
    public string Xml { get; set; } = "";
    public byte[]? Pdf { get; set; }
    public Guid? SolicitacaoId { get; set; }
    public string HashSolicitacao { get; set; } = "";
    public string XmlEnvioNfe { get; set; } = "";
    public string XmlRetornoAutorizacao { get; set; } = "";
    public string Recibo { get; set; } = "";
    public string CodigoAutorizacao { get; set; } = "";
    public string MotivoAutorizacao { get; set; } = "";
    public DateTime? AutorizadaEm { get; set; }
    public int UsuarioEmissorId { get; set; }
    public List<NotaFiscalItem> Itens { get; set; } = [];
    public List<NotaFiscalEvento> Eventos { get; set; } = [];
}

public class NotaFiscalEvento
{
    public long Id { get; set; }
    public long NotaFiscalId { get; set; }
    public NotaFiscal NotaFiscal { get; set; } = null!;
    public string Tipo { get; set; } = "";
    public int Sequencia { get; set; }
    public string Texto { get; set; } = "";
    public string Situacao { get; set; } = "Pendente";
    public string Codigo { get; set; } = "";
    public string Motivo { get; set; } = "";
    public string Protocolo { get; set; } = "";
    public string XmlEnvio { get; set; } = "";
    public string XmlRetorno { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

public sealed class NotaFiscalItem
{
    public long Id { get; set; }
    public long NotaFiscalId { get; set; }
    public NotaFiscal NotaFiscal { get; set; } = null!;
    public int ProdutoId { get; set; }
    public CadastroItens Produto { get; set; } = null!;
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public double CustoUnitario { get; set; }
}

public sealed class NumeracaoNfe
{
    public int EmpresaId { get; set; }
    public int Ambiente { get; set; }
    public int Serie { get; set; }
    public int ProximoNumero { get; set; } = 1;
}