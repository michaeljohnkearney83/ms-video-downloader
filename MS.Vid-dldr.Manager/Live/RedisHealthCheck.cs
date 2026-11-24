using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace MS.Vid_dldr.Manager.Live;

/// <summary>
/// Redis carries live progress only, so losing it makes the Manager Degraded, not Unhealthy: downloads still run,
/// the Updater sends every tick to Postgres instead, and browsers fall back to polling.
/// </summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failureStatus = context.Registration?.FailureStatus ?? HealthStatus.Degraded;
        if (!redis.IsConnected)
            return new HealthCheckResult(failureStatus, "Not connected to Redis: live progress is off, browsers poll");
        try
        {
            var latency = await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy($"Redis answered in {latency.TotalMilliseconds:0.#} ms",
                new Dictionary<string, object> { ["latencyMs"] = Math.Round(latency.TotalMilliseconds, 1) });
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(failureStatus, $"Redis did not answer: {ex.Message}", ex);
        }
    }
}
