using System.Security.Claims;
using back.Models;

namespace back.Services;

// Only called with the principal validated by the OIDC middleware.
public static class HubCompanyProfile
{
    public static void Aplicar(Empresas empresa, ClaimsPrincipal principal)
    {
        string? Read(string field) => principal.FindFirstValue("tenant_" + field)?.Trim() is { Length: > 0 } value ? value : null;
        empresa.RazaoSocial = Read("razao_social") ?? Read("name") ?? empresa.RazaoSocial;
        empresa.NomeFantasia = Read("nome_fantasia") ?? Read("name") ?? empresa.NomeFantasia;
        if (Read("cnpj") is { } document)
        {
            var digits = NfeGerador.Digitos(document);
            if (digits.Length != 14) throw new InvalidOperationException("CNPJ recebido do HUB invalido.");
            empresa.Cnpj = digits;
        }
        empresa.InscricaoEstadual = Read("inscricao_estadual") ?? empresa.InscricaoEstadual;
        empresa.Endereco = Read("logradouro") ?? empresa.Endereco;
        empresa.Numero = Read("numero") ?? empresa.Numero;
        empresa.Complemento = Read("complemento") ?? empresa.Complemento;
        empresa.Bairro = Read("bairro") ?? empresa.Bairro;
        empresa.Cidade = Read("municipio") ?? empresa.Cidade;
        empresa.Estado = Read("uf")?.ToUpperInvariant() ?? empresa.Estado;
        empresa.Cep = Read("cep") is { } cep ? NfeGerador.Digitos(cep) : empresa.Cep;
    }
}
