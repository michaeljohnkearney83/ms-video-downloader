using System.Diagnostics;
using MassTransit;
using MS.Vid_dldr.Manager.Dtos;

namespace MS.Vid_dldr.Manager.Messaging;

public class MassTransitDownloadRequestPublisher : IDownloadRequestPublisher
{
    /// <summary>W3C trace context headers. The Python Downloader reads them to continue the trace.</summary>
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<MassTransitDownloadRequestPublisher> _logger;
    private readonly Uri _queueAddress;
    private readonly Uri _controlAddress;

    public MassTransitDownloadRequestPublisher(ISendEndpointProvider sendEndpointProvider, IAppSettings appSettings,
        ILogger<MassTransitDownloadRequestPublisher> logger)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _logger = logger;
        _queueAddress = MessagingSetup.GetQueueAddress(appSettings.QueueRequestDownload);
        _controlAddress = MessagingSetup.GetControlExchangeAddress(appSettings.ExchangeDownloadControl);
    }

    public async Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class, IMessageDto
    {
        _logger.LogInformation("Sending {MessageType} to {QueueAddress}", message.MessageType, _queueAddress);

        var endpoint = await _sendEndpointProvider.GetSendEndpoint(_queueAddress);
        await endpoint.Send(message, context => AddTraceContext(context.Headers), cancellationToken);
    }

    public async Task SendControlAsync(DownloadControlDto message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending {Action} for download {DownloadId} to {ControlAddress}", message.Action, message.Id, _controlAddress);

        var endpoint = await _sendEndpointProvider.GetSendEndpoint(_controlAddress);
        await endpoint.Send(message, context => AddTraceContext(context.Headers), cancellationToken);
    }

    /// <summary>Copies the current W3C trace context (MassTransit's send span when it is recorded) into the headers.</summary>
    internal static void AddTraceContext(SendHeaders headers)
    {
        if (Activity.Current is not { IdFormat: ActivityIdFormat.W3C } activity) return;

        headers.Set(TraceParentHeader, activity.Id);
        if (!string.IsNullOrEmpty(activity.TraceStateString))
            headers.Set(TraceStateHeader, activity.TraceStateString);
    }
}
