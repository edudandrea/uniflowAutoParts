using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class NfeTestDatabase
{
    public static async Task<DbContextOptions<AppDbContext>> CreateAsync(params int[] empresas)
    {
        var name = $"nfe-checks-{Guid.NewGuid()}";
        var root = new InMemoryDatabaseRoot();
        await using var directory = new HubDirectoryDbContext(new DbContextOptionsBuilder<HubDirectoryDbContext>()
            .UseInMemoryDatabase(name, root).Options);
        foreach (var id in empresas.Length > 0 ? empresas : [1])
        {
            directory.Empresas.Add(new Empresas { Id = id, RazaoSocial = "Empresa de teste", Cnpj = id.ToString().PadLeft(14, '0') });
            directory.Users.Add(new Users { Id = id, EmpresaId = id, Nome = "Usuario de teste", Email = $"{id}@example.test" });
        }
        await directory.SaveChangesAsync();
        return new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name, root).Options;
    }
}
