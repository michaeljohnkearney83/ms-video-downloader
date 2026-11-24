using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MS.Vid_dldr.Manager.Health;

/// <summary>
/// Health endpoints for the Manager and the pipeline behind it.
/// <list type="bullet">
/// <item><c>/health/live</c>: the process answers. No dependency checks, so a database outage doesn't make Docker
/// restart a Manager that is fine.</item>
/// <item><c>/health/ready</c>: Postgres, the MassTransit bus (RabbitMQ), and a consumer on the Downloader's and the
/// DownloadUpdater's queues. JSON report; HTTP 503 when Unhealthy, 200 when Healthy or Degraded.</item>
/// </list>
/// Postgres and RabbitMQ failing make the Manager Unhealthy (it cannot take requests). A missing worker only makes it
/// Degraded: requests are accepted and wait in the queue.
/// </summary>
public static class HealthSetup
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string ReadyTag = "ready";

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    public static IServiceCollection AddVdHealthChecks(this IServiceCollection services)
    {
        services.AddHttpClient(RabbitMqQueueConsumersCheck.HttpClientName, (sp, client) =>
        {
            var settings = sp.GetRequiredService<IAppSettings>();
            client.BaseAddress = new Uri($"http://{settings.ChannelHostName}:{settings.ChannelManagementPort}/");
            client.Timeout = CheckTimeout;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ChannelUsername}:{settings.ChannelPassword}")));
        });

        services.AddHttpClient(DownloaderWorkerCheck.HttpClientName, client => client.Timeout = CheckTimeout);

        // MassTransit adds its own "masstransit-bus" check (tagged "ready") once health checks are registered.
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag], CheckTimeout)
            .Add(new HealthCheckRegistration("downloader",
                sp =>
                {
                    var settings = sp.GetRequiredService<IAppSettings>();
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    return new DownloaderWorkerCheck(
                        new RabbitMqQueueConsumersCheck(httpClientFactory, settings.QueueRequestDownload, "Downloader"),
                        httpClientFactory, settings.DownloaderHealthUrl);
                },
                HealthStatus.Degraded, [ReadyTag], CheckTimeout))
            .Add(new HealthCheckRegistration("updater",
                sp => new RabbitMqQueueConsumersCheck(sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<IAppSettings>().QueueDownloadUpdate, "DownloadUpdater"),
                HealthStatus.Degraded, [ReadyTag], CheckTimeout));

        // Every check answers within 3 s. Without this the database check runs EF's retry-on-failure strategy
        // (5 retries, up to 10 s apart) and /health/ready took 66 s to say Unhealthy when Postgres was down.
        // Cancelling at the timeout also stops those retries.
        services.PostConfigure<HealthCheckServiceOptions>(o =>
        {
            foreach (var registration in o.Registrations)
                if (registration.Timeout == Timeout.InfiniteTimeSpan || registration.Timeout > CheckTimeout)
                    registration.Timeout = CheckTimeout;
        });

        // Run the ready checks in the background too: HealthMetricsPublisher turns them into a metric and logs changes.
        services.Configure<HealthCheckPublisherOptions>(o =>
        {
            o.Delay = TimeSpan.FromSeconds(5);
            o.Period = TimeSpan.FromSeconds(15);
            o.Predicate = IsReady;
        });
        services.AddSingleton<IHealthCheckPublisher, HealthMetricsPublisher>();

        return services;
    }

    public static bool IsReady(HealthCheckRegistration registration) => registration.Tags.Contains(ReadyTag);

    public static void MapVdHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteJsonAsync })
            .RequireCors("AllowAll");
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = IsReady, ResponseWriter = WriteJsonAsync })
            .RequireCors("AllowAll");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>{ status, totalDurationMs, checks: [{ name, status, description, durationMs, error, data }] }</summary>
    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                error = e.Value.Exception?.Message,
                data = e.Value.Data.Count == 0 ? null : e.Value.Data,
            }),
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions);
    }
}
