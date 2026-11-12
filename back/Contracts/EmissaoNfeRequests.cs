namespace back.Contracts;

public sealed record EmitirNfeItemRequest(int ProdutoId, decimal Quantidade, decimal PrecoUnitario, decimal DescontoPercentual, string? Origem = null, string? Ncm = null, string? Unidade = null);
public sealed record EmitirNfePagamentoRequest(string Forma, decimal Valor);
public sealed record EmitirNfeRequest(Guid SolicitacaoId, long ClienteId, int Ambiente, int Serie, string Natureza,
    string Cfop, string Modelo, decimal Desconto, decimal Frete, decimal Despesas, string? Observacoes,
    IReadOnlyList<EmitirNfeItemRequest> Itens, IReadOnlyList<EmitirNfePagamentoRequest> Pagamentos,
    string Base64, string Password, DestinatarioFiscalRequest? Destinatario = null, int ModalidadeFrete = 9, int IndicadorPresenca = 1);
public sealed record EmissaoNfeResponse(long Id, string Numero, string Serie, string ChaveAcesso, int Ambiente,
    string Situacao, string Codigo, string Motivo, string Protocolo, bool PodeReenviar);
public sealed record EmitenteFiscalRequest(string RazaoSocial, string InscricaoEstadual, string Logradouro,
    string Numero, string? Complemento, string Bairro, string Municipio, string CodigoMunicipio, string Uf,
    string Cep, int Serie, int ProximoNumero);
public sealed record EmitenteFiscalResponse(string Cnpj, EmitenteFiscalRequest Dados);
public sealed record DestinatarioFiscalRequest(string InscricaoEstadual, int IndicadorIe, bool ConsumidorFinal,
    string Logradouro, string Numero, string? Complemento, string Bairro, string Municipio, string CodigoMunicipio, string Uf, string Cep);
