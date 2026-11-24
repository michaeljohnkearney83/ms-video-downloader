using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Vid_dldr.Manager.Data;

namespace MS.Vid_dldr.Manager.Health;

/// <summary>
/// Can the Manager reach Postgres? Like AddDbContextCheck, but a timeout (the registration's 3 s cancels the token,
/// which also stops EF's retry-on-failure strategy) reads as a sentence instead of "The operation was canceled".
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseHealthCheck(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failureStatus = context.Registration?.FailureStatus ?? HealthStatus.Unhealthy;
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Postgres is reachable")
                : new HealthCheckResult(failureStatus, "Cannot connect to Postgres");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var timeout = context.Registration?.Timeout;
            var limit = timeout is { } t && t != Timeout.InfiniteTimeSpan ? $" within {t.TotalSeconds:0.#} s" : "";
            return new HealthCheckResult(failureStatus, $"No answer from Postgres{limit}");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(failureStatus, $"Cannot connect to Postgres: {ex.Message}", ex);
        }
    }
}
