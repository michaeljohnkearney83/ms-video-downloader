using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MS.Vid_dldr.DownloadUpdater.Progress;

public interface IProgressStore
{
    /// <summary>Stores a DOWNLOADING tick and publishes it. False when Redis is unavailable.</summary>
    Task<bool> WriteProgressAsync(ProgressEvent progress);

    /// <summary>Publishes a state change (ANALYSIS, FINISHED, ERRORED, ...) and drops the live hash once it ends.</summary>
    Task PublishStateChangeAsync(ProgressEvent change);
}

/// <summary>
/// Live progress in Redis. Hash <c>vd:progress:{id}</c> (expires <see cref="Ttl"/> after the last tick) for "what is it at
/// now", and a PUBLISH on <see cref="Channel"/> so every Manager instance can push the tick to its browsers.
/// Nothing here is the system of record: Postgres is, via the Manager.
/// </summary>
public sealed class RedisProgressStore(IConnectionMultiplexer redis, ILogger<RedisProgressStore> logger) : IProgressStore
{
    public const string Channel = "vd:progress";
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public static string Key(int downloadId) => $"vd:progress:{downloadId}";

    private static readonly RedisChannel PublishChannel = RedisChannel.Literal(Channel);

    public async Task<bool> WriteProgressAsync(ProgressEvent progress)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = Key(progress.Id);
            // One round trip: HSET + EXPIRE + PUBLISH in a single transaction-less batch.
            var batch = db.CreateBatch();
            var set = batch.HashSetAsync(key,
            [
                new("status", progress.Status),
                new("percent", progress.Percent ?? ""),
                new("speed", progress.Speed ?? ""),
                new("eta", progress.Eta ?? ""),
                new("updatedUtc", progress.UpdatedUtc.ToString("O")),
            ]);
            var expire = batch.KeyExpireAsync(key, Ttl);
            var publish = batch.PublishAsync(PublishChannel, progress.ToJson());
            batch.Execute();
            await Task.WhenAll(set, expire, publish);
            return true;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis unavailable; progress for download {DownloadId} goes to the Manager only", progress.Id);
            return false;
        }
    }

    public async Task PublishStateChangeAsync(ProgressEvent change)
    {
        try
        {
            var db = redis.GetDatabase();
            if (change.Status is "FINISHED" or "ERRORED")
                await db.KeyDeleteAsync(Key(change.Id));
            await db.PublishAsync(PublishChannel, change.ToJson());
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Redis unavailable; browsers will see {Status} for download {DownloadId} on their next poll",
                change.Status, change.Id);
        }
    }
}
