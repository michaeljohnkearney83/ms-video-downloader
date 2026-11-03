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
    private IConnection _rMqConnection;
    private IModel _rMqChannel;

    public UpdaterService(IAppSettings appSettings, ILoggerFactory loggerFactory)
    {
        _appSettings = appSettings;
        _loggerFactory = loggerFactory;
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

        _rMqConnection.ConnectionShutdown += RabbitMQ_ConnectionShutdown;
    }

    private void OnConsumerConsumerCancelled(object sender, ConsumerEventArgs e) { }
    private void OnConsumerUnregistered(object sender, ConsumerEventArgs e) { }
    private void OnConsumerRegistered(object sender, ConsumerEventArgs e) { }
    private void OnConsumerShutdown(object sender, ShutdownEventArgs e) { }
    private void RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e) { }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[CALL] StartAsync(CancellationToken cancellationToken)");

        while (!cancellationToken.IsCancellationRequested)
        {
            //Manger API setup with transient fault handling to send download updates:
            var apiChannel = GrpcChannel.ForAddress(_appSettings.ManagerApiAddress, new GrpcChannelOptions
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

            var client = new VdApiCallsForUpdater.VdApiCallsForUpdaterClient(apiChannel);

            //Process RabbitMq messages & send call to Manager based on status:
            var consumer = new EventingBasicConsumer(_rMqChannel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[INFO] Message Received: {message}");

                var downloadUpdateDto = JsonSerializer.Deserialize<DownloadUpdateDto>(message);

                try
                {
                    switch (downloadUpdateDto.Status)
                    {
                        case Constantss.DownloadStatusTypeAnalysis:
                            await client.UpdateDownloadProgressAnalysisAsync(
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
                            await client.UpdateDownloadProgressDownloadingAsync(
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
                            await client.UpdateDownloadProgressFinishedAsync(
                                new UpdateDownloadProgressFinishedRequest
                                {
                                    Id = downloadUpdateDto.Id,
                                    LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadUpdateDto.LastActivityUtc),
                                    Status = downloadUpdateDto.Status
                                });
                            break;
                        case Constantss.DownloadStatusTypeErrored:
                            await client.UpdateDownloadProgressErroredAsync(
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

                            await client.UpdateRequestExtractInfoFormatsAsync(
                                new UpdateRequestExtractInfoFormatsRequest
                                {
                                    Id = updateRequestExtractDto.Id,
                                    ExtractInfo = extractInfoProto
                                });
                            break;
                        case Constantss.VersionMessageType:
                            var setVersionsDto = JsonSerializer.Deserialize<SetVersionsDto>(message);

                            Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[INFO] Versions; {setVersionsDto.ToString()}");

                            await client.SetVersionsAsync(
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
                            Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[WARN] Action not supported: {downloadUpdateDto.Status}");
                            break;
                    }
                }
                catch (RpcException ex)
                {
                    if (ex.StatusCode == StatusCode.Unavailable)
                    {
                        //Cant figure out subchannel drop no matter how much transient & retries, the service stays up & I cant figure a way to listen to the retry flag etc.
                        //If grpc loses connection with subchannel aka. throw status Unavailable, exits for a Docker restart & it will continue process progress from the Updater.
                        //eg. "Picked failure with status: Status(StatusCode="Unavailable", Detail="Error connecting to subchannel.", DebugException="System.Net.Sockets.SocketException: Cannot assign requested address")"
                        //Seems to be a Docker/Host/DNS issue, you can be downloading all day then it stops connecting to the Manager.
                        //UPDATE: Think it was the way I wasnt keeping the factory open after every message.
                        Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[WARN] Restarting via Docker on service exit...");
                        System.Environment.Exit(1);
                    }
                    else
                    {
                        Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[EXCEPTION] Errored: {ex.ToString()}");
                    }
                }
            };

            consumer.Shutdown += OnConsumerShutdown;
            consumer.Registered += OnConsumerRegistered;
            consumer.Unregistered += OnConsumerUnregistered;
            consumer.ConsumerCancelled += OnConsumerConsumerCancelled;

            _rMqChannel.BasicConsume(_appSettings.QueueDownloadUpdate, true, consumer);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[CALL] StopAsync(CancellationToken cancellationToken)");

        _rMqChannel.Close();
        _rMqConnection.Close();

        return Task.CompletedTask;
    }
}