using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record ConfiguracaoTributariaRequest(
    [Required, RegularExpression("^[1-4]$")] string Regime,
    [Required] string IcmsCode,
    [Required, RegularExpression("^(0[1-9]|49|5[0-6]|6[0-7]|7[0-5]|98|99)$")] string PisCode,
    [Required, RegularExpression("^(0[1-9]|49|5[0-6]|6[0-7]|7[0-5]|98|99)$")] string CofinsCode,
    [Range(typeof(decimal), "0", "100")] decimal? IcmsRate,
    [Range(typeof(decimal), "0", "100")] decimal? PisRate,
    [Range(typeof(decimal), "0", "100")] decimal? CofinsRate) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        string[] codes = Regime is "1" or "4"
            ? ["101", "102", "103", "201", "202", "203", "300", "400", "500", "900"]
            : ["00", "02", "10", "15", "20", "30", "40", "41", "50", "51", "53", "60", "61", "70", "90"];
        if (!codes.Contains(IcmsCode))
            yield return new ValidationResult("Informe um codigo de ICMS valido para o regime tributario.", [nameof(IcmsCode)]);
        if (new[] { IcmsRate, PisRate, CofinsRate }.Any(rate => rate.HasValue && decimal.Round(rate.Value, 4) != rate.Value))
            yield return new ValidationResult("Informe aliquotas com ate quatro casas decimais.");
    }
}

