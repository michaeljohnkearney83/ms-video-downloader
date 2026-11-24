using StackExchange.Redis;

namespace MS.Vid_dldr.Manager.Live;

public interface ILiveProgressStore
{
    /// <summary>Latest tick per download id. Empty when Redis is unavailable (the list then shows the checkpoint).</summary>
    Task<IReadOnlyDictionary<int, LiveProgress>> GetAsync(IReadOnlyCollection<int> downloadIds);

    /// <summary>Drops a download's live values (after pause / resume / cancel, so they can't outlive the change).</summary>
    Task ClearAsync(int downloadId);
}

public sealed class RedisLiveProgressStore(IConnectionMultiplexer redis, ILogger<RedisLiveProgressStore> logger) : ILiveProgressStore
{
    private static readonly RedisValue[] Fields = ["status", "percent"];

    public async Task<IReadOnlyDictionary<int, LiveProgress>> GetAsync(IReadOnlyCollection<int> downloadIds)
    {
        var result = new Dictionary<int, LiveProgress>();
        if (downloadIds.Count == 0 || !redis.IsConnected) return result;
        try
        {
            var db = redis.GetDatabase();
            var batch = db.CreateBatch();
            var reads = downloadIds.Select(id => (id, task: batch.HashGetAsync(LiveProgressSetup.Key(id), Fields))).ToList();
            batch.Execute();
            await Task.WhenAll(reads.Select(r => r.task));
            foreach (var (id, task) in reads)
            {
                var values = task.Result;
                if (!values[0].IsNullOrEmpty)
                    result[id] = new LiveProgress(values[0].ToString(), values[1].IsNullOrEmpty ? null : values[1].ToString());
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis unavailable; listing downloads from Postgres only");
        }
        return result;
    }

    public async Task ClearAsync(int downloadId)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(LiveProgressSetup.Key(downloadId)); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not clear live progress for download {DownloadId}", downloadId);
        }
    }
}
