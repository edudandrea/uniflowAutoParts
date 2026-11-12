using System.Security.Claims;
using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Services;

public sealed class HubIdentityService(HubDirectoryDbContext db, IConfiguration config)
{
    public async Task<(Empresas Empresa, Users Usuario)> VincularAsync(ClaimsPrincipal principal, string issuer, CancellationToken token)
    {
        var subject = principal.FindFirstValue("sub");
        var tenantId = principal.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 200 || string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > 200)
            throw new InvalidOperationException("O HUB nao informou os identificadores do usuario e da empresa.");
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({issuer + "|" + tenantId}, 0))", token);
        var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.HubIssuer == issuer && e.HubTenantId == tenantId, token);
        var profile = config.GetSection("HubAuth:Companies").GetChildren().FirstOrDefault(c => c["TenantId"] == tenantId);
        var binding = profile?.GetValue<int?>("LocalEmpresaId") ?? config.GetValue<int?>($"HubAuth:CompanyBindings:{tenantId}");
        if (empresa is null && binding.HasValue)
        {
            empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == binding.Value, token)
                ?? throw new InvalidOperationException("A empresa vinculada ao HUB nao existe no AutoParts.");
            if (empresa.HubTenantId is not null && (empresa.HubTenantId != tenantId || empresa.HubIssuer != issuer))
                throw new InvalidOperationException("Esta empresa ja esta vinculada a outro cliente do HUB.");
        }
        var companyName = principal.FindFirstValue("tenant_name") ?? profile?["Name"] ?? config[$"HubAuth:CompanyProfiles:{tenantId}:Name"];
        var companyDocument = principal.FindFirstValue("tenant_cnpj") ?? profile?["Cnpj"] ?? config[$"HubAuth:CompanyProfiles:{tenantId}:Cnpj"];
        if (empresa is null)
        {
            empresa = new Empresas { RazaoSocial = companyName?.Trim() ?? "", NomeFantasia = companyName?.Trim() ?? "" };
            db.Empresas.Add(empresa);
        }
        if (!empresa.Ativo || empresa.AcessoBloqueado) throw new InvalidOperationException("O acesso desta empresa ao AutoParts esta bloqueado.");
        empresa.HubIssuer = issuer;
        empresa.HubTenantId = tenantId;
        if (!string.IsNullOrWhiteSpace(companyName)) empresa.NomeFantasia = empresa.RazaoSocial = companyName.Trim();
        if (!string.IsNullOrWhiteSpace(companyDocument))
        {
            var cnpj = new string(companyDocument.Where(char.IsAsciiDigit).ToArray());
            if (cnpj.Length != 14) throw new InvalidOperationException("CNPJ recebido do HUB invalido.");
            empresa.Cnpj = cnpj;
        }
        HubCompanyProfile.Aplicar(empresa, principal);
        await db.SaveChangesAsync(token);
        // A identidade do HUB, e nao o email, define o vinculo. Senhas nao sao copiadas.
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmpresaId == empresa.Id && u.HubSubject == subject, token);
        if (user is null)
        {
            user = new Users { EmpresaId = empresa.Id, HubSubject = subject, Perfil = PerfilUsuario.Operador };
            db.Users.Add(user);
        }
        if (!user.Ativo) throw new InvalidOperationException("Usuario desativado no AutoParts.");
        user.Nome = principal.FindFirstValue("name") ?? "";
        user.Email = principal.FindFirstValue("email") ?? "";
        user.EmailNormalizado = user.Email.Trim().ToUpperInvariant();
        user.UltimoAcessoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return (empresa, user);
    }
}
