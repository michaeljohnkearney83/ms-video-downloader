using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MS.Vid_dldr.Manager.Observability;
using StackExchange.Redis;

namespace MS.Vid_dldr.Manager.Live;

/// <summary>
/// Subscribes to the Updater's progress channel and pushes each event to the browsers connected to THIS Manager
/// instance. Every instance subscribes, so each browser hears every event once, whichever instance it is connected to.
/// (That is why there is no SignalR Redis backplane: it would deliver each event once per instance.)
/// </summary>
public sealed class RedisProgressRelay(
    IConnectionMultiplexer redis,
    IHubContext<DownloadsHub> hub,
    DownloadLookup lookup,
    ILogger<RedisProgressRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = redis.GetSubscriber();
        // A ChannelMessageQueue handles messages one at a time, in order (ticks for a download never overtake).
        // StackExchange.Redis re-subscribes by itself after a reconnect.
        var queue = await subscriber.SubscribeAsync(RedisChannel.Literal(LiveProgressSetup.Channel));
        queue.OnMessage(message => RelayAsync(message.Message, stoppingToken));
        logger.LogInformation("Relaying {Channel} to SignalR clients", LiveProgressSetup.Channel);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        finally { await queue.UnsubscribeAsync(); }
    }

    private async Task RelayAsync(RedisValue payload, CancellationToken cancellationToken)
    {
        try
        {
            var progress = JsonSerializer.Deserialize<ProgressEvent>(payload.ToString());
            if (progress == null) return;

            var target = await lookup.FindAsync(progress.Id, cancellationToken);
            if (target == null) return;

            var isTick = progress.Status == Constants.DownloadStatusTypeDownloading;
            await hub.Clients.Group(DownloadsHub.Group(target.SessionId.ToString())).SendAsync(
                isTick ? LiveProgressSetup.ProgressMethod : LiveProgressSetup.ChangedMethod,
                new LiveDownloadMessage(target.ClientId.ToString(), progress.Status, progress.Percent, progress.Speed, progress.Eta),
                cancellationToken);

            Telemetry.LivePushes.Add(1, new KeyValuePair<string, object?>("vd.live.kind", isTick ? "progress" : "changed"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not relay progress message {Payload}", payload.ToString());
        }
    }
}
