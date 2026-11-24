using MS.Vid_dldr.Manager.Dtos;

namespace MS.Vid_dldr.Manager.Messaging;

/// <summary>Sends work (download, analysis, extract info, versions) to the Downloader's request queue.</summary>
public interface IDownloadRequestPublisher
{
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IMessageDto;
}
