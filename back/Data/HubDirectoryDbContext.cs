using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Data;

// Diretorio usado exclusivamente na autenticacao: nao expoe cadastros comerciais.
public sealed class HubDirectoryDbContext(DbContextOptions<HubDirectoryDbContext> options) : DbContext(options)
{
    public DbSet<Empresas> Empresas => Set<Empresas>();
    public DbSet<Users> Users => Set<Users>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Empresas>().ToTable("Empresas");
        model.Entity<Empresas>().Property(e => e.HubTenantId).IsConcurrencyToken();
        model.Entity<Users>().ToTable("Users");
        model.Entity<Users>().HasOne(u => u.Empresa).WithMany().HasForeignKey(u => u.EmpresaId);
    }
}
