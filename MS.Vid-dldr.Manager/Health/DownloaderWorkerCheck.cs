using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MS.Vid_dldr.Manager.Health;

/// <summary>
/// The <c>downloader</c> check. First the queue: is anything consuming the request queue (crashed / not started)?
/// Then the Downloader's own <c>/health</c> (Python, port 8081): what is it doing, and is a job stuck? That catches a
/// Downloader that is connected but hung, which the consumer count can't see.
/// </summary>
public sealed class DownloaderWorkerCheck : IHealthCheck
{
    public const string HttpClientName = "downloader-health";

    private readonly IHealthCheck _queueCheck;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _healthUrl;

    /// <param name="healthUrl">The Downloader's /health. Empty = check the queue only (e.g. a Manager outside Docker).</param>
    public DownloaderWorkerCheck(IHealthCheck queueCheck, IHttpClientFactory httpClientFactory, string? healthUrl)
    {
        _queueCheck = queueCheck;
        _httpClientFactory = httpClientFactory;
        _healthUrl = healthUrl;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var queue = await _queueCheck.CheckHealthAsync(context, cancellationToken);
        if (queue.Status != HealthStatus.Healthy || string.IsNullOrWhiteSpace(_healthUrl))
            return queue;

        var failureStatus = context.Registration?.FailureStatus ?? HealthStatus.Degraded;
        var data = new Dictionary<string, object>(queue.Data);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(_healthUrl, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.ServiceUnavailable)
                return new HealthCheckResult(failureStatus,
                    $"{queue.Description}, but its health endpoint returned HTTP {(int)response.StatusCode}", data: data);

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var root = json.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var description = root.TryGetProperty("description", out var d) ? d.GetString() : null;

            foreach (var name in new[] { "jobsSucceeded", "jobsFailed", "lastFailure", "ytdlpVersion" })
                if (root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
                    data[name] = value.ValueKind == JsonValueKind.Number ? value.GetInt32() : value.ToString();
            if (root.TryGetProperty("job", out var job) && job.ValueKind == JsonValueKind.Object)
                data["job"] = job.ToString();

            return status == "Healthy"
                ? HealthCheckResult.Healthy(description ?? queue.Description, data)
                : new HealthCheckResult(failureStatus, description ?? $"Downloader reports {status}", data: data);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(failureStatus,
                $"{queue.Description}, but its health endpoint doesn't answer: {ex.Message}", ex, data);
        }
    }
}
