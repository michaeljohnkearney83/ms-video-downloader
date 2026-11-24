using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Messaging;

namespace MS.Vid_dldr.Manager.Tests;

/// <summary>Records messages instead of sending them to RabbitMQ.</summary>
public class FakeDownloadRequestPublisher : IDownloadRequestPublisher
{
    public List<IMessageDto> Sent { get; } = new();
    public Exception? ThrowOnSend { get; set; }

    public Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IMessageDto
    {
        if (ThrowOnSend != null) throw ThrowOnSend;
        Sent.Add(message);
        return Task.CompletedTask;
    }

    public List<DownloadControlDto> Controls { get; } = new();

    public Task SendControlAsync(DownloadControlDto message, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend != null) throw ThrowOnSend;
        Controls.Add(message);
        return Task.CompletedTask;
    }
}
