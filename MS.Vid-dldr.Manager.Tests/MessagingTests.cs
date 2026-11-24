using System.Diagnostics;
using System.Text.Json.Nodes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Messaging;

namespace MS.Vid_dldr.Manager.Tests;

/// <summary>
/// The Python Downloader reads the request queue with pika and json.loads, so MassTransit must put the same JSON
/// on the wire that the old RabbitMQ.Client producer sent (DTO.ToString()). These tests send through the real
/// publisher on the in-memory transport and compare the raw body.
/// </summary>
public class MessagingTests
{
    private const string QueueName = "ms_vd_q_downloadRequest";

    /// <summary>Any JSON object; the consumer only looks at the raw body.</summary>
    public class RawMessage { }

    public class RawBodyConsumer : IConsumer<RawMessage>
    {
        public static readonly List<string> Bodies = new();
        public static readonly List<string?> TraceParents = new();

        public Task Consume(ConsumeContext<RawMessage> context)
        {
            lock (Bodies)
            {
                Bodies.Add(context.ReceiveContext.Body.GetString());
                TraceParents.Add(context.Headers.Get<string>(MassTransitDownloadRequestPublisher.TraceParentHeader));
            }
            return Task.CompletedTask;
        }
    }

    private static async Task<string> SendAndCaptureAsync<T>(T message) where T : class, IMessageDto
    {
        lock (RawBodyConsumer.Bodies) { RawBodyConsumer.Bodies.Clear(); RawBodyConsumer.TraceParents.Clear(); }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppSettings:QueueRequestDownload"] = QueueName })
            .Build();

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IAppSettings>(new AppSettings(configuration))
            .AddScoped<IDownloadRequestPublisher, MassTransitDownloadRequestPublisher>()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<RawBodyConsumer>();
                x.UsingInMemory((context, cfg) =>
                {
                    MessagingSetup.ConfigureSerialization(cfg);
                    cfg.ReceiveEndpoint(QueueName, e =>
                    {
                        // Like the Python Downloader, read every body whatever its message type header says.
                        e.UseRawJsonDeserializer(RawSerializerOptions.AnyMessageType, isDefault: true);
                        e.ConfigureConsumer<RawBodyConsumer>(context);
                    });
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IDownloadRequestPublisher>().SendAsync(message);

        var consumed = await harness.Consumed.Any<RawMessage>();
        Assert.True(consumed, "message was not received on the request queue. Sent: " + string.Join("; ",
            harness.Sent.Select(_ => true).Select(m => $"{m.MessageType.Name} -> {m.Context.DestinationAddress}"))
            + " | Consumed: " + string.Join("; ", harness.Consumed.Select(_ => true).Select(m => m.MessageType.Name + " " + m.Exception?.Message)));

        lock (RawBodyConsumer.Bodies) return Assert.Single(RawBodyConsumer.Bodies);
    }

    private static void AssertSameJson(string expected, string actual) =>
        Assert.Equal(JsonNode.Parse(expected)!.ToJsonString(), JsonNode.Parse(actual)!.ToJsonString());

    [Fact]
    public async Task DownloadRequest_IsSentAsTheSameRawJsonAsBefore()
    {
        var dto = new DownloadRequestDto
        {
            Id = 42,
            ClientId = Guid.Parse("a1b2c3d4-e5f6-4789-abcd-ef0123456789"),
            RequestedUrl = "https://example.com/watch?v=abc&t=1",
            RequestedDownloadName = "video",
            RequestedDownloadExt = "mp4",
            RequestedRetryCount = "3",
            RequestedTimeoutSeconds = "600",
            RequestedDownloadStalledTimeoutSeconds = null!,
        };

        var body = await SendAndCaptureAsync(dto);

        AssertSameJson(dto.ToString(), body);
        var json = JsonNode.Parse(body)!.AsObject();
        Assert.Equal("DOWNLOAD", json["messageType"]!.GetValue<string>());
        // Nulls are kept, as the old JsonSerializer.Serialize(this) did.
        Assert.True(json.ContainsKey("downloadStalledTimeoutSeconds"));
    }

    [Fact]
    public async Task AnalysisRequest_IsSentAsTheSameRawJsonAsBefore()
    {
        var dto = new AnalysisRequestDto
        {
            Id = 1, ClientId = Guid.NewGuid(), RequestedUrl = "https://example.com", IsRetry = true,
        };

        AssertSameJson(dto.ToString(), await SendAndCaptureAsync(dto));
    }

    [Fact]
    public async Task ExtractInfoRequest_IsSentAsTheSameRawJsonAsBefore()
    {
        var dto = new ExtractInfoRequestDto { Id = 9, RequestedUrl = "https://example.com/v" };

        AssertSameJson(dto.ToString(), await SendAndCaptureAsync(dto));
    }

    [Fact]
    public async Task SetVersions_IsSentAsTheSameRawJsonAsBefore()
    {
        var dto = new SetVersionsDto
        {
            ManagerApp = "2026.327.16.0",
            ManagerFramework = ".NET 7.0",
            Requested = new DateTime(2026, 11, 23, 16, 0, 0, DateTimeKind.Utc),
        };

        AssertSameJson(dto.ToString(), await SendAndCaptureAsync(dto));
    }

    [Fact]
    public async Task Send_InsideATrace_AddsW3CTraceParentWithTheSameTraceId()
    {
        using var activity = new Activity("RequestDownload").SetIdFormat(ActivityIdFormat.W3C).Start();

        await SendAndCaptureAsync(new ExtractInfoRequestDto { Id = 1, RequestedUrl = "https://example.com" });

        string? traceParent;
        lock (RawBodyConsumer.Bodies) traceParent = Assert.Single(RawBodyConsumer.TraceParents);
        Assert.NotNull(traceParent);
        // 00-<trace id>-<span id>-<flags>
        Assert.Equal(activity.TraceId.ToString(), traceParent!.Split('-')[1]);
    }

    [Fact]
    public void QueueAddress_IsTheNonDurableDownloaderQueue()
    {
        Assert.Equal("queue:ms_vd_q_downloadRequest?durable=false", MessagingSetup.GetQueueAddress(QueueName).ToString());
    }
}
