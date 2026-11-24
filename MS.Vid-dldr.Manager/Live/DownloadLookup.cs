using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MS.Vid_dldr.Manager.Data;

namespace MS.Vid_dldr.Manager.Live;

/// <summary>Download id -> (public id, session). Progress events carry only the id; this never changes, so it is cached.</summary>
public sealed class DownloadLookup(IServiceScopeFactory scopes)
{
    public sealed record Target(Guid ClientId, Guid SessionId);

    private readonly ConcurrentDictionary<int, Target> _cache = new();

    public async Task<Target?> FindAsync(int downloadId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(downloadId, out var cached)) return cached;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var found = await db.Downloads.AsNoTracking()
            .Where(d => d.Id == downloadId)
            .Select(d => new Target(d.ClientId, d.DownloadSessionId))
            .FirstOrDefaultAsync(cancellationToken);

        if (found != null)
        {
            if (_cache.Count > 10_000) _cache.Clear();
            _cache[downloadId] = found;
        }
        return found;
    }
}
