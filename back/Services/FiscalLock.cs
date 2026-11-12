using System.Collections.Concurrent;
using back.Data;
using Microsoft.EntityFrameworkCore;

namespace back.Services;

public sealed class FiscalLock : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> Local = new();
    private readonly AppDbContext db;
    private readonly long key;
    private readonly SemaphoreSlim? semaphore;
    private FiscalLock(AppDbContext db, long key, SemaphoreSlim? semaphore) { this.db = db; this.key = key; this.semaphore = semaphore; }
    public static async Task<FiscalLock?> AdquirirAsync(AppDbContext db, long key, CancellationToken token)
    {
        if (!db.Database.IsNpgsql())
        {
            var semaphore = Local.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            return await semaphore.WaitAsync(0, token) ? new FiscalLock(db, key, semaphore) : null;
        }
        await db.Database.OpenConnectionAsync(token);
        try
        {
            if (await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({key}) AS \"Value\"").SingleAsync(token))
                return new FiscalLock(db, key, null);
            await db.Database.CloseConnectionAsync();
            return null;
        }
        catch { await db.Database.CloseConnectionAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        if (semaphore is not null) { semaphore.Release(); return; }
        try { await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({key})", CancellationToken.None); }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}