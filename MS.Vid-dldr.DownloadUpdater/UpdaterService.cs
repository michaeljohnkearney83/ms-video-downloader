using MS.Vid_dldr.DownloadUpdater.Observability;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Grpc.Core;
using Grpc.Net.Client;
using MS.Vid_dldr.DownloadUpdater.Dtos;
using MS.Vid_dldr.Manager.Protos;
using Microsoft.Extensions.Hosting;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Logging;
using MS.Vid_dldr.DownloadUpdater.Helpers;
using System.Runtime.InteropServices;
using MS.Vid_dldr.DownloaderUpdater.Mappers;

namespace MS.Vid_dldr.DownloadUpdater;

public class UpdaterService : IHostedService
{
    private static IAppSettings _appSettings;
    private static ILoggerFactory _loggerFactory;
    private readonly ILogger<UpdaterService> _logger;
    private IConnection _rMqConnection;
    private IModel _rMqChannel;
    private GrpcChannel _apiChannel;
    private VdApiCallsForUpdater.VdApiCallsForUpdaterClient _client;

    public UpdaterService(IAppSettings appSettings, ILoggerFactory loggerFactory)
    {
        _appSettings = appSettings;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<UpdaterService>();
        ConfigureDownloaderConsumer();
    }

    private void ConfigureDownloaderConsumer()
    {
        var connectionFactory = new ConnectionFactory { HostName = _appSettings.ChannelHostName, Port = _appSettings.ChannelPortNumber };
        _rMqConnection = connectionFactory.CreateConnection();
        _rMqChannel = _rMqConnection.CreateModel();

        _rMqChannel.QueueDeclare(
               queue: _appSettings.QueueDownloadUpdate,
               durable: false,
               exclusive: false,
               autoDelete: false,
               arguments: null);
        _rMqChannel.BasicQos(0, 1, false);

        _rMqConnection.ConnectionShutdown += (_, e) =>
            _logger.LogWarning("RabbitMQ connection shut down: {ReplyText}", e.ReplyText);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting: consuming {Queue}, forwarding to the Manager at {ManagerApiAddress}",
            _appSettings.QueueDownloadUpdate, _appSettings.ManagerApiAddress);

        // One channel and one consumer for the life of the service. (This used to sit in a
        // `while (!cancellationToken.IsCancellationRequested)` loop, so StartAsync never returned and kept registering
        // consumers and gRPC channels: hundreds of thousands of consumers and RabbitMQ at ~190% CPU.)
        _apiChannel = GrpcChannel.ForAddress(_appSettings.ManagerApiAddress, new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.Insecure,
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            },
            ServiceConfig = new ServiceConfig
            {
                MethodConfigs = {
                    new MethodConfig
                    {
                        Names = { MethodName.Default },
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = 5,
                            InitialBackoff = TimeSpan.FromSeconds(1),
                            MaxBackoff = TimeSpan.FromSeconds(5),
                            BackoffMultiplier = 1.5,
                            RetryableStatusCodes = { StatusCode.Unavailable },
                        }
                    }
                }
            },
            LoggerFactory = _loggerFactory,
        });
        _client = new VdApiCallsForUpdater.VdApiCallsForUpdaterClient(_apiChannel);

        var consumer = new EventingBasicConsumer(_rMqChannel);
        consumer.Received += async (_, ea) => await HandleMessageAsync(ea);
        _rMqChannel.BasicConsume(_appSettings.QueueDownloadUpdate, true, consumer);

        // The OpenTelemetry hosted service is registered (and started) before this one.
        Telemetry.InitialiseCounters();

        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        using var activity = Telemetry.StartConsumeActivity(ea.BasicProperties?.Headers, _appSettings.QueueDownloadUpdate);
        var started = Stopwatch.GetTimestamp();
        var status = "UNKNOWN";

        try
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var downloadUpdateDto = JsonSerializer.Deserialize<DownloadUpdateDto>(message);
            status = downloadUpdateDto.Status ?? "UNKNOWN";

            activity?.SetTag(Telemetry.DownloadIdTag, downloadUpdateDto.Id);
            activity?.SetTag(Telemetry.StatusTag, status);
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                [Telemetry.DownloadIdTag] = downloadUpdateDto.Id,
                [Telemetry.StatusTag] = status,
            });
            _logger.LogDebug("Message received: {Message}", message);

            await ForwardAsync(downloadUpdateDto, message);

            _logger.LogInformation("Forwarded {Status} for download {DownloadId} to the Manager", status, downloadUpdateDto.Id);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Status.Detail);
            // The gRPC subchannel can stay broken after a Manager restart; exit so Docker restarts the service.
            _logger.LogCritical(ex, "Manager unavailable; exiting so Docker restarts the service");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Failed to forward {Status} to the Manager", status);
        }
        finally
        {
            var tags = new KeyValuePair<string, object>(Telemetry.StatusTag, status);
            Telemetry.MessagesProcessed.Add(1, tags);
            Telemetry.ProcessingDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        }
    }

    private async Task ForwardAsync(DownloadUpdateDto downloadUpdateDto, string message)
    {
        switch (downloadUpdateDto.Status)
        {
            case Constantss.DownloadStatusTypeAnalysis:
                await _client.UpdateDownloadProgressAnalysisAsync(
                   new UpdateDownloadProgressAnalysisRequest
                   {
                       Id = downloadUpdateDto.Id,
                       VideoId = downloadUpdateDto.VideoId,
                       VideoTitle = downloadUpdateDto.VideoTitle,
                       VideoDescription = downloadUpdateDto.VideoDescription,
                       VideoDuration = downloadUpdateDto.VideoDuration,
                       VideoThumbnail = downloadUpdateDto.VideoThumbnail,
                       VideoUrl = downloadUpdateDto.VideoUrl,
                       VideoResolution = downloadUpdateDto.VideoResolution,
                       VideoAspecRatio = downloadUpdateDto.VideoAspectRatio,
                       VideoFileSizeApprox = downloadUpdateDto.VideoFilesizeApprox,
                       LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadUpdateDto.LastActivityUtc),
                       Status = downloadUpdateDto.Status
                   });
                break;
            case Constantss.DownloadStatusTypeDownloading:
                await _client.UpdateDownloadProgressDownloadingAsync(
                    new UpdateDownloadProgressDownloadingRequest
                    {
                        Id = downloadUpdateDto.Id,
                        LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadUpdateDto.LastActivityUtc),
                        Status = downloadUpdateDto.Status,
                        ProgressEta = downloadUpdateDto.ProgressEta,
                        ProgressSpeed = downloadUpdateDto.ProgressSpeed,
                        ProgressPercent = downloadUpdateDto.ProgressPercent,
                        ProgressElapsed = downloadUpdateDto.ProgressElapsed,
                        ProgressDefaultTemplate = downloadUpdateDto.ProgressDefaultTemplate,
                        Filename = downloadUpdateDto.Filename
                    });
                break;
            case Constantss.DownloadStatusTypeFinished:
                await _client.UpdateDownloadProgressFinishedAsync(
                    new UpdateDownloadProgressFinishedRequest
                    {
                        Id = downloadUpdateDto.Id,
                        LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadUpdateDto.LastActivityUtc),
                        Status = downloadUpdateDto.Status
                    });
                break;
            case Constantss.DownloadStatusTypeErrored:
                await _client.UpdateDownloadProgressErroredAsync(
                    new UpdateDownloadProgressErroredRequest
                    {
                        Id = downloadUpdateDto.Id,
                        LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadUpdateDto.LastActivityUtc),
                        Status = downloadUpdateDto.Status,
                        ErrorMessage = downloadUpdateDto.ErrorMessage
                    });
                break;
            case Constantss.RequestExtractInfoMessageType:
                var updateRequestExtractDto = JsonSerializer.Deserialize<UpdateRequestExtractDto>(message);

                var extractInfoProto = ConfigureVdMapper.Map(updateRequestExtractDto.ExtractInfo);

                await _client.UpdateRequestExtractInfoFormatsAsync(
                    new UpdateRequestExtractInfoFormatsRequest
                    {
                        Id = updateRequestExtractDto.Id,
                        ExtractInfo = extractInfoProto
                    });
                break;
            case Constantss.VersionMessageType:
                var setVersionsDto = JsonSerializer.Deserialize<SetVersionsDto>(message);

                _logger.LogInformation("Versions: {Versions}", setVersionsDto.ToString());

                await _client.SetVersionsAsync(
                    new SetVersionsRequest
                    {
                        ManagerApp = setVersionsDto.ManagerApp,
                        ManagerFramework = setVersionsDto.ManagerFramework,
                        ManagerDb = setVersionsDto.ManagerDb,
                        ManagerDbLastMigration = setVersionsDto.ManagerDbLastMigration,
                        DownloaderApp = setVersionsDto.DownloaderApp,
                        DownloaderFramework = setVersionsDto.DownloaderFramework,
                        DownloaderYtdlp = setVersionsDto.DownloaderYtdlp,
                        UpdaterApp = UtilityHelper.GetVersion(),
                        UpdaterFramework = RuntimeInformation.FrameworkDescription,
                        Requested = setVersionsDto.Requested,
                    });
                break;
            case Constantss.DownloadStatusTypeNone:
            case Constantss.DownloadStatusTypeRequested:
            case Constantss.DownloadStatusTypeQueued:
            case Constantss.DownloadStatusTypeDistributedLookup:
            case Constantss.DownloadStatusTypeDistributed:
            default:
                _logger.LogWarning("Action not supported: {Status}", downloadUpdateDto.Status);
                break;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping");

        _rMqChannel.Close();
        _rMqConnection.Close();
        _apiChannel?.Dispose();

        return Task.CompletedTask;
    }
}
