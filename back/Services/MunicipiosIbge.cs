namespace back.Services;

internal static class MunicipiosIbge
{
    internal sealed record Municipio(string CodigoIbge, string Uf, string Nome);

    internal static readonly IReadOnlyDictionary<string, Municipio> PorCodigo = Ler();
    private static readonly IReadOnlyDictionary<string, string> PrefixosPorUf = PorCodigo.Values
        .GroupBy(municipio => municipio.Uf)
        .ToDictionary(grupo => grupo.Key, grupo => grupo.First().CodigoIbge[..2]);

    internal static bool UfValida(string? uf) => uf is not null && PrefixosPorUf.ContainsKey(uf);

    internal static bool CodigoCorrespondeUf(string? codigo, string? uf) =>
        codigo is { Length: 7 } && codigo.All(char.IsAsciiDigit) &&
        uf is not null && PrefixosPorUf.TryGetValue(uf, out var prefixo) &&
        codigo.StartsWith(prefixo, StringComparison.Ordinal);

    private static Dictionary<string, Municipio> Ler()
    {
        // Fonte: https://servicodados.ibge.gov.br/api/v1/localidades/municipios?view=nivelado
        using var stream = typeof(MunicipiosIbge).Assembly.GetManifestResourceStream("municipios-ibge.csv")
            ?? throw new InvalidOperationException("Base de municípios do IBGE não encontrada.");
        using var reader = new StreamReader(stream);
        var municipios = new Dictionary<string, Municipio>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } linha)
        {
            var campos = linha.Split(';', 3);
            municipios.Add(campos[0], new Municipio(campos[0], campos[1], campos[2]));
        }
        return municipios;
    }
}
