using System.Linq.Expressions;
using System.Reflection;
using back.Models;
using back.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace back.Data;

public partial class AppDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ValidateTenantWrites(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task ValidateTenantWrites(CancellationToken token)
    {
        ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if (entries.Length == 0) return;
        if (CurrentEmpresaId <= 0) throw new TenantAccessException();
        foreach (var entry in entries)
        {
            if (entry.Entity is Empresas company)
            {
                if (entry.State != EntityState.Modified || company.Id != CurrentEmpresaId ||
                    entry.Property(nameof(back.Models.Empresas.HubIssuer)).IsModified || entry.Property(nameof(back.Models.Empresas.HubTenantId)).IsModified)
                    throw new TenantAccessException();
            }
            var owner = entry.Metadata.FindProperty("EmpresaId") ?? entry.Metadata.FindProperty("TenantId");
            if (owner is not null)
            {
                var property = entry.Property(owner.Name);
                var current = property.CurrentValue is null ? 0 : Convert.ToInt64(property.CurrentValue);
                if (entry.State == EntityState.Added && current == 0)
                    property.CurrentValue = Convert.ChangeType(CurrentEmpresaId, Nullable.GetUnderlyingType(owner.ClrType) ?? owner.ClrType);
                else if (current != CurrentEmpresaId) throw new TenantAccessException();
                if (entry.State != EntityState.Added && Convert.ToInt64(property.OriginalValue) != CurrentEmpresaId)
                    throw new TenantAccessException();
            }
        }
        var verified = new HashSet<string>();
        foreach (var entry in entries)
        {
            if (entry.State != EntityState.Added)
            {
                var primary = entry.Metadata.FindPrimaryKey()!;
                if (!await Exists(entry.Metadata, primary.Properties, primary.Properties.Select(p => entry.Property(p.Name).CurrentValue).ToArray()))
                    throw new TenantAccessException();
            }
            foreach (var fk in entry.Metadata.GetForeignKeys())
            {
                var values = fk.Properties.Select(p => entry.Property(p.Name).CurrentValue).ToArray();
                if (values.Any(v => v is null)) continue;
                var added = entries.Any(candidate => candidate.State == EntityState.Added && candidate.Metadata == fk.PrincipalEntityType &&
                    fk.PrincipalKey.Properties.Select(p => candidate.Property(p.Name).CurrentValue).SequenceEqual(values));
                if (!added && !await Exists(fk.PrincipalEntityType, fk.PrincipalKey.Properties, values)) throw new TenantAccessException();
            }
            // Estes identificadores ainda nao possuem FK configurada no modelo legado.
            if (entry.Entity is Estoque stock && (!await Exists(Model.FindEntityType(typeof(CadastroItens))!,
                    Model.FindEntityType(typeof(CadastroItens))!.FindPrimaryKey()!.Properties, [stock.ItemId]) ||
                !await Exists(Model.FindEntityType(typeof(Users))!, Model.FindEntityType(typeof(Users))!.FindPrimaryKey()!.Properties, [stock.UsuarioId])))
                throw new TenantAccessException();
        }

        async Task<bool> Exists(IEntityType type, IReadOnlyList<IProperty> keys, object?[] values)
        {
            if (entries.Any(candidate => candidate.State == EntityState.Added && candidate.Metadata == type &&
                keys.Select(p => candidate.Property(p.Name).CurrentValue).SequenceEqual(values))) return true;
            var cache = type.Name + ":" + string.Join("|", values);
            if (verified.Contains(cache)) return true;
            var method = typeof(AppDbContext).GetMethod(nameof(ExistsFiltered), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(type.ClrType);
            var result = await (Task<bool>)method.Invoke(this, [keys, values, token])!;
            if (result) verified.Add(cache);
            return result;
        }
    }

    private Task<bool> ExistsFiltered<TEntity>(IReadOnlyList<IProperty> keys, object?[] values, CancellationToken token) where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        Expression expression = Expression.Constant(true);
        for (var i = 0; i < keys.Count; i++)
        {
            var property = Expression.Call(typeof(EF), nameof(EF.Property), [keys[i].ClrType], parameter, Expression.Constant(keys[i].Name));
            var constant = Expression.Convert(Expression.Constant(values[i]), keys[i].ClrType);
            expression = Expression.AndAlso(expression, Expression.Equal(property, constant));
        }
        return Set<TEntity>().AsNoTracking().AnyAsync(Expression.Lambda<Func<TEntity, bool>>(expression, parameter), token);
    }
}
