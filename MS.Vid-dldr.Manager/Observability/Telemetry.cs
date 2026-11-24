using System.Diagnostics;
using System.Diagnostics.Metrics;
using MS.Vid_dldr.Manager.Helpers;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentry;

namespace MS.Vid_dldr.Manager.Observability;

/// <summary>
/// OpenTelemetry for the Manager: traces, metrics and logs over OTLP (OTEL_EXPORTER_OTLP_ENDPOINT, default
/// http://localhost:4317), plus Sentry error tracking when SENTRY_DSN is set.
/// The W3C trace id is the cross-service correlation id. The download's own ids are added as span tags and log
/// properties so a download can also be found when the trace is broken (e.g. the Distributor's file-system hop).
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "ms-vid-dldr-manager";

    public static readonly ActivitySource Source = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    /// <summary>Manager database id of a download (int).</summary>
    public const string DownloadIdTag = "vd.download.id";
    /// <summary>Public id of a download (GUID), also the file name the Downloader writes.</summary>
    public const string ClientIdTag = "vd.download.client_id";
    public const string StatusTag = "vd.download.status";

    public static readonly Counter<long> DownloadsRequested =
        Meter.CreateCounter<long>("vd.downloads.requested", description: "Downloads queued for the Downloader");

    public static readonly Counter<long> StatusUpdates =
        Meter.CreateCounter<long>("vd.download.status_updates", description: "Status updates received from the Downloader");

    public static readonly Counter<long> DownloadActions =
        Meter.CreateCounter<long>("vd.download.actions", description: "Pause / resume / cancel requests applied");

    public static readonly Counter<long> LivePushes =
        Meter.CreateCounter<long>("vd.live.pushes", description: "Progress events pushed to browsers over SignalR");

    public const string TraceIdHeader = "X-Trace-Id";

    private static readonly string[] Statuses = ["ANALYSIS", "DOWNLOADING", "FINISHED", "ERRORED", "EXTRACT_INFO"];

    /// <summary>
    /// Call after the host has started (.NET drops measurements made before the MeterProvider listens).
    /// Reports each counter series at 0. Prometheus' increase() needs a starting sample, so without this the first
    /// increment after a restart (or of a rare status) never shows on the dashboard.
    /// </summary>
    public static void InitialiseCounters()
    {
        DownloadsRequested.Add(0);
        LivePushes.Add(0, new KeyValuePair<string, object?>("vd.live.kind", "progress"));
        LivePushes.Add(0, new KeyValuePair<string, object?>("vd.live.kind", "changed"));
        foreach (var status in Statuses)
            StatusUpdates.Add(0, new KeyValuePair<string, object?>(StatusTag, status));
    }

    public static WebApplicationBuilder AddVdTelemetry(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? ServiceName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: UtilityHelper.GetVersion())
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(t => t
                .AddSource(Source.Name)
                .AddSource("MassTransit")
                // Health probes (Docker, the web app, the background publisher) would bury real requests in Tempo.
                .AddAspNetCoreInstrumentation(o => o.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage = request =>
                    request.RequestUri?.AbsolutePath.StartsWith("/api/queues/") != true)
                .AddNpgsql()
                .AddRedisInstrumentation())
            .WithMetrics(m => m
                .AddMeter(Meter.Name)
                .AddMeter("MassTransit")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddNpgsqlInstrumentation())
            .WithLogging(_ => { }, o =>
            {
                o.IncludeFormattedMessage = true;
                o.IncludeScopes = true;
            })
            .UseOtlpExporter();

        var sentryDsn = builder.Configuration["SENTRY_DSN"];
        if (!string.IsNullOrWhiteSpace(sentryDsn))
        {
            builder.WebHost.UseSentry(o =>
            {
                o.Dsn = sentryDsn;
                o.Environment = builder.Environment.EnvironmentName;
                o.Release = $"{serviceName}@{UtilityHelper.GetVersion()}";
                o.SendDefaultPii = false;
                // Tracing stays in OpenTelemetry; tag each Sentry event with the trace id to jump to Tempo.
                o.TracesSampleRate = 0;
                o.SetBeforeSend((sentryEvent, _) =>
                {
                    if (Activity.Current is { } activity)
                        sentryEvent.SetTag("otel.trace_id", activity.TraceId.ToString());
                    return sentryEvent;
                });
            });
        }

        return builder;
    }

    /// <summary>Returns the request's trace id in X-Trace-Id, so a user or the web app can quote it.</summary>
    public static IApplicationBuilder UseTraceIdResponseHeader(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (Activity.Current is { } activity)
                    context.Response.Headers[TraceIdHeader] = activity.TraceId.ToString();
                return Task.CompletedTask;
            });
            await next();
        });

    /// <summary>Tags the current span with the download and counts the status update.</summary>
    public static void RecordStatusUpdate(int downloadId, string status)
    {
        Activity.Current?.SetTag(DownloadIdTag, downloadId);
        Activity.Current?.SetTag(StatusTag, status);
        StatusUpdates.Add(1, new KeyValuePair<string, object?>(StatusTag, status));
    }
}
