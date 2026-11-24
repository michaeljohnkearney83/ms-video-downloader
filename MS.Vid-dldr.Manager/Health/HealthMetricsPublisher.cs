using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Vid_dldr.Manager.Observability;

namespace MS.Vid_dldr.Manager.Health;

/// <summary>
/// Receives the background health report every 15 s. It exposes each check as the gauge <c>vd.health.status</c>
/// (2 = Healthy, 1 = Degraded, 0 = Unhealthy; label <c>vd.health.check</c>, plus "overall") for dashboards and
/// alerts, and logs each status change, so a failing dependency shows up in Loki and, as an error, in Sentry.
/// </summary>
public sealed class HealthMetricsPublisher : IHealthCheckPublisher
{
    public const string Overall = "overall";

    private readonly ConcurrentDictionary<string, HealthStatus> _statuses = new();
    private readonly ILogger<HealthMetricsPublisher> _logger;

    public HealthMetricsPublisher(ILogger<HealthMetricsPublisher> logger)
    {
        _logger = logger;
        Telemetry.Meter.CreateObservableGauge("vd.health.status", Observe,
            description: "Health check status: 2 Healthy, 1 Degraded, 0 Unhealthy");
    }

    public IReadOnlyDictionary<string, HealthStatus> Statuses => _statuses;

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var (name, entry) in report.Entries)
            Record(name, entry.Status, entry.Description, entry.Exception);
        Record(Overall, report.Status, null, null);
        return Task.CompletedTask;
    }

    private void Record(string name, HealthStatus status, string? description, Exception? exception)
    {
        var known = _statuses.TryGetValue(name, out var previous);
        _statuses[name] = status;
        if (known && previous == status) return;

        var level = status switch
        {
            HealthStatus.Unhealthy => LogLevel.Error,
            HealthStatus.Degraded => LogLevel.Warning,
            _ => LogLevel.Information,
        };
        _logger.Log(level, exception, "Health check {HealthCheck} is {HealthStatus} (was {PreviousHealthStatus}): {Description}",
            name, status, known ? previous.ToString() : "unknown", description ?? "");
    }

    private IEnumerable<System.Diagnostics.Metrics.Measurement<int>> Observe() =>
        _statuses.Select(s => new System.Diagnostics.Metrics.Measurement<int>((int)s.Value,
            new KeyValuePair<string, object?>("vd.health.check", s.Key)));
}
