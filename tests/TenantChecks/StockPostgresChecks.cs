using System.Data.Common;
using System.Data;
using back.Contracts;
using back.Controllers;
using back.Data;
using back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class StockPostgresChecks
{
    public static async Task RunAsync()
    {
        var tenant = new TenantContext(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = ChecksAuthentication.Principal(1) }
        });
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=query-compilation-only;Username=unused;Password=unused")
            .AddInterceptors(new SuppressConnection(), new ProductWithoutMovement()).Options;
        await using var db = new AppDbContext(options, tenant);
        var result = await new EstoqueController(db).Listar(1, CancellationToken.None);
        var produtos = (IReadOnlyCollection<EstoqueResponse>)((OkObjectResult)result.Result!).Value!;
        if (produtos.Count != 1 || produtos.Single().Estoque != 0)
            throw new Exception("Produto sem movimentos deve ser retornado com saldo zero.");
        Console.WriteLine("Stock PostgreSQL materialization check passed. Nenhuma conexao ao banco foi aberta.");
    }

    private sealed class SuppressConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class ProductWithoutMovement : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            // Simula a linha do LEFT JOIN sem movimento. O provedor real compila
            // e materializa a consulta; somente o transporte ao PostgreSQL e substituido.
            var projection = command.CommandText.Split("\nFROM", StringSplitOptions.None)[0];
            var columns = new List<(Type Type, object Value)>
            {
                (typeof(int), 17), (typeof(string), "1700"), (typeof(string), "Produto importado da NF-e"),
                (typeof(string), "Não informada"), (typeof(string), "Importados de NF-e")
            };
            if (projection.Contains("e0.\"ItemId\"")) columns.Add((typeof(int), DBNull.Value));
            columns.Add((typeof(int), projection.Contains("COALESCE(e0.\"Quantidade\", 0)") ? 0 : DBNull.Value));
            columns.AddRange([(typeof(int), 0), (typeof(double), 0d), (typeof(string), ""), (typeof(string), "")]);
            var table = new DataTable();
            foreach (var column in columns) table.Columns.Add("col" + table.Columns.Count, column.Type);
            table.Rows.Add(columns.Select(c => c.Value).ToArray());
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }
    }
}
