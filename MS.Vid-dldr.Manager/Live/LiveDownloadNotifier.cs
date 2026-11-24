using Microsoft.AspNetCore.SignalR;
using MS.Vid_dldr.Manager.Entities;

namespace MS.Vid_dldr.Manager.Live;

/// <summary>Tells a session's browsers that a download changed in the Manager itself (pause / resume / cancel).</summary>
public interface ILiveDownloadNotifier
{
    Task DownloadChangedAsync(Download download, CancellationToken cancellationToken = default);
}

public sealed class LiveDownloadNotifier(IHubContext<DownloadsHub> hub, ILiveProgressStore store, ILogger<LiveDownloadNotifier> logger)
    : ILiveDownloadNotifier
{
    public async Task DownloadChangedAsync(Download download, CancellationToken cancellationToken = default)
    {
        await store.ClearAsync(download.Id);
        try
        {
            await hub.Clients.Group(DownloadsHub.Group(download.DownloadSessionId.ToString()))
                .SendAsync(LiveProgressSetup.ChangedMethod,
                    new LiveDownloadMessage(download.ClientId.ToString(), download.Status, download.ProgressPercent, null, null),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            // Browsers still catch up on their next poll.
            logger.LogWarning(ex, "Could not push {Status} for download {DownloadId}", download.Status, download.Id);
        }
    }
}
