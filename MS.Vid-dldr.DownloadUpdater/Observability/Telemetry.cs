using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;


namespace MS.Vid_dldr.DownloadUpdater.Observability;

/// <summary>
/// OpenTelemetry for the DownloadUpdater: traces, metrics and logs over OTLP (OTEL_EXPORTER_OTLP_ENDPOINT), plus Sentry
/// for error logs when SENTRY_DSN is set. RabbitMQ.Client 6 has no instrumentation, so the consumer continues the
/// Downloader's trace from the W3C traceparent header itself (<see cref="StartConsumeActivity"/>).
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "ms-vid-dldr-updater";

    public static readonly ActivitySource Source = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // Explicit W3C propagator: Propagators.DefaultTextMapPropagator is a no-op until the SDK has started.
    private static readonly TextMapPropagator Propagator =
        new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]);

    public const string DownloadIdTag = "vd.download.id";
    public const string StatusTag = "vd.download.status";

    public static readonly Counter<long> MessagesProcessed =
        Meter.CreateCounter<long>("vd.updater.messages", description: "Progress messages forwarded to the Manager");

    public static readonly Histogram<double> ProcessingDuration =
        Meter.CreateHistogram<double>("vd.updater.processing.duration", unit: "s",
            description: "Time to forward one progress message to the Manager");

    public static readonly Counter<long> ManagerCallsSkipped =
        Meter.CreateCounter<long>("vd.updater.progress.skipped",
            description: "DOWNLOADING ticks sent to Redis only (not to the Manager/Postgres)");

    public static IServiceCollection AddVdTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? ServiceName;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: Helpers.UtilityHelper.GetVersion()))
            .WithTracing(t => t
                .AddSource(Source.Name)
                // The gRPC client uses HttpClient: this records the call and sends traceparent to the Manager.
                .AddHttpClientInstrumentation()
                .AddRedisInstrumentation())
            .WithMetrics(m => m
                .AddMeter(Meter.Name)
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithLogging(_ => { }, o =>
            {
                o.IncludeFormattedMessage = true;
                o.IncludeScopes = true;
            })
            .UseOtlpExporter();

        var sentryDsn = configuration["SENTRY_DSN"];
        if (!string.IsNullOrWhiteSpace(sentryDsn))
        {
            // Sentry 7: the logging integration no longer starts the SDK, so start it here, then send error logs to it.
            Sentry.SentrySdk.Init(o =>
            {
                o.Dsn = sentryDsn;
                o.Release = $"{serviceName}@{Helpers.UtilityHelper.GetVersion()}";
                o.SetBeforeSend((sentryEvent, _) =>
                {
                    if (Activity.Current is { } activity)
                        sentryEvent.SetTag("otel.trace_id", activity.TraceId.ToString());
                    return sentryEvent;
                });
            });
            services.AddLogging(logging => logging.AddSentry());
        }

        return services;
    }

    /// <summary>
    /// Reports each counter series at 0 so Prometheus' increase() counts the first real message. Call after the host
    /// has started: .NET drops measurements made before the MeterProvider listens.
    /// </summary>
    public static void InitialiseCounters()
    {
        foreach (var status in new[] { "ANALYSIS", "DOWNLOADING", "FINISHED", "ERRORED", "EXTRACT_INFO", "VERSION" })
            MessagesProcessed.Add(0, new KeyValuePair<string, object>(StatusTag, status));
        ManagerCallsSkipped.Add(0);
    }

    /// <summary>Starts a consumer span that continues the trace in the message's traceparent header, if any.</summary>
    public static Activity? StartConsumeActivity(IDictionary<string, object>? headers, string queue)
    {
        var parent = Propagator.Extract(default, headers, ReadHeader);
        Baggage.Current = parent.Baggage;

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.operation.type", "process");
        return activity;
    }

    // RabbitMQ.Client returns string header values as byte[].
    public static IEnumerable<string> ReadHeader(IDictionary<string, object>? headers, string key)
    {
        if (headers == null || !headers.TryGetValue(key, out var value) || value == null) return [];
        return [value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value.ToString()!];
    }
}
