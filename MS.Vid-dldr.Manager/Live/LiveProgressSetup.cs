using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Vid_dldr.Manager.Health;
using StackExchange.Redis;

namespace MS.Vid_dldr.Manager.Live;

/// <summary>
/// Live download progress: Redis (written by the DownloadUpdater) -> this Manager -> browsers over SignalR.
/// RabbitMQ still carries the work (requests, updates, control); Postgres is still the record.
/// </summary>
public static class LiveProgressSetup
{
    public const string HubPath = "/hubs/downloads";
    public const string Channel = "vd:progress";
    public const string ProgressMethod = "progress";
    public const string ChangedMethod = "changed";

    public static string Key(int downloadId) => $"vd:progress:{downloadId}";

    public static IServiceCollection AddLiveProgress(this IServiceCollection services, IConfiguration configuration)
    {
        // abortConnect=false: the Manager starts without Redis and reconnects in the background.
        var connection = configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connection));
        services.AddSingleton<ILiveProgressStore, RedisLiveProgressStore>();
        services.AddSingleton<DownloadLookup>();
        services.AddSingleton<ILiveDownloadNotifier, LiveDownloadNotifier>();
        services.AddHostedService<RedisProgressRelay>();
        services.AddSignalR();
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, [HealthSetup.ReadyTag]);
        return services;
    }

    public static void MapLiveProgress(this IEndpointRouteBuilder app) => app.MapHub<DownloadsHub>(HubPath);
}
