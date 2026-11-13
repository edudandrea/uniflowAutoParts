namespace back.Contracts;

public record NotaFiscalResponse(long Id, string Numero, string Serie, DateTime Emissao,
    string Cliente, string DocumentoCliente, string Tipo, string Situacao, decimal Valor,
    string ChaveAcesso, int Ambiente, bool TemPdf, bool PodeCancelar, bool PodeCorrigir,
    string? BloqueioEventos, bool PodeReenviar = false, string CodigoAutorizacao = "", string MotivoAutorizacao = "", string Protocolo = "");
public record NotaFiscalResumo(int Emitidas, int Autorizadas, int Processando, int Canceladas, decimal ValorTotal);
public record NotaFiscalListaResponse(int Pagina, int TamanhoPagina, int Total,
    NotaFiscalResumo Resumo, IReadOnlyCollection<NotaFiscalResponse> Notas);
public record NotaFiscalEventoRequest(string Texto, string Base64, string Password, bool ConfirmarNaoCirculacao = false);
public record NotaFiscalEventoResponse(string Codigo, string Motivo, string Protocolo, int Sequencia);
