using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record AtualizarMargemPrecoRequest(
    [Required] decimal? MargemPreco);
