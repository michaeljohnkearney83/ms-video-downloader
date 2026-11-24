using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MS.Vid_dldr.Ytdlp.Observability;

/// <summary>
/// OpenTelemetry for the Ytdlp version checker: traces, metrics and logs over OTLP (OTEL_EXPORTER_OTLP_ENDPOINT, default
/// http://localhost:4317), plus Sentry for error logs when SENTRY_DSN is set. HttpClient calls to the Manager carry
/// the W3C traceparent, so the Manager's spans join this service's trace.
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "ms-vid-dldr-ytdlp";

    public static readonly ActivitySource Source = new(ServiceName);

    public const string ClientIdTag = "vd.download.client_id";

    /// <summary>Set once the host is built. Until then (and in unit tests) WriteLog goes to the console.</summary>
    public static ILogger? Logger { get; set; }

    public static IServiceCollection AddVdTelemetry(this IServiceCollection services, IConfiguration configuration, string version)
    {
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? ServiceName;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: version))
            .WithTracing(t => t
                .AddSource(Source.Name)
                .AddHttpClientInstrumentation())
            .WithMetrics(m => m
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
            Sentry.SentrySdk.Init(o =>
            {
                o.Dsn = sentryDsn;
                o.Release = $"{serviceName}@{version}";
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

    /// <summary>The service's existing WriteLog(status, line), sent to ILogger (and so to Loki) once available.</summary>
    public static void Log(string status, string line)
    {
        if (Logger == null)
        {
            Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[{status}] {line}");
            return;
        }

        var level = status switch
        {
            Constantss.LogStatusErr or Constantss.LogStatusEx => LogLevel.Error,
            Constantss.LogStatusWarn => LogLevel.Warning,
            Constantss.LogStatusCall or Constantss.LogStatusReq or Constantss.LogStatusRes or Constantss.LogStatusRet => LogLevel.Debug,
            _ => LogLevel.Information,
        };
        Logger.Log(level, "[{Status}] {Line}", status, line);
    }
}
