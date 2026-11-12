using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record AmbienteFiscalRequest(
    [Required, RegularExpression("^(homologacao|producao)$")] string Environment);
public sealed record AmbienteFiscalResponse(string Environment, bool EmissaoDisponivel = true, int SeriePadrao = 1);