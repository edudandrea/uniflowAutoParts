using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Data;

public partial class AppDbContext
{
    // A mesma regra vale para cadastro manual, importação e atualizações de custo/preço.
    private async Task ApplyGlobalStockMargin(CancellationToken token)
    {
        ChangeTracker.DetectChanges();
        var products = ChangeTracker.Entries<CadastroItens>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        if (products.Length == 0) return;
        var company = Empresas.Local.FirstOrDefault(e => e.Id == CurrentEmpresaId);
        var margin = company is not null ? company.MargemPrecoEstoque :
            await Empresas.AsNoTracking().Where(e => e.Id == CurrentEmpresaId).Select(e => e.MargemPrecoEstoque).SingleOrDefaultAsync(token);
        if (margin is null) return; // Preserva preços existentes até a primeira configuração explícita.
        foreach (var entry in products)
        {
            entry.Entity.MargemPreco = margin;
            entry.Entity.AtualizarPrecoPelaMargem();
        }
    }
}
