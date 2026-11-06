using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace back.Data;

// Gerar migrations nao precisa iniciar a API nem acessar um banco de producao.
public class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=uniflow_design;Username=design;Password=design")
            .Options;
        return new AppDbContext(options);
    }
}
