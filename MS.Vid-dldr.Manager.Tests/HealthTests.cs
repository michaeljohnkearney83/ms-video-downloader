using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MS.Vid_dldr.Manager.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MS.Vid_dldr.Manager.Health;

namespace MS.Vid_dldr.Manager.Tests;

public class HealthTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://rabbitmq:15672/") };
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static async Task<(HealthCheckResult Result, StubHandler Handler)> CheckAsync(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var check = new RabbitMqQueueConsumersCheck(new StubFactory(handler), "ms_vd_q_downloadRequest", "Downloader", maxExpectedConsumers: 10);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("downloader", check, HealthStatus.Degraded, null),
        };
        return (await check.CheckHealthAsync(context), handler);
    }

    [Fact]
    public async Task QueueCheck_WithAConsumer_IsHealthy()
    {
        var (result, handler) = await CheckAsync(_ => Json("""{"consumers":1,"messages":0}"""));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(1, result.Data["consumers"]);
        Assert.Equal("/api/queues/%2F/ms_vd_q_downloadRequest", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task QueueCheck_NoConsumer_IsDegradedAndSaysHowManyMessagesWait()
    {
        var (result, _) = await CheckAsync(_ => Json("""{"consumers":0,"messages":4}"""));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("No Downloader is consuming ms_vd_q_downloadRequest (4 message(s) waiting)", result.Description);
    }

    [Fact]
    public async Task QueueCheck_TooManyConsumers_FlagsALeak()
    {
        var (result, _) = await CheckAsync(_ => Json("""{"consumers":258283,"messages":0}"""));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("possible consumer leak", result.Description);
    }

    [Fact]
    public async Task QueueCheck_QueueMissing_SaysTheWorkerNeverConnected()
    {
        var (result, _) = await CheckAsync(_ => Json("""{"error":"Object Not Found"}""", HttpStatusCode.NotFound));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("has not connected yet", result.Description);
    }

    [Fact]
    public async Task QueueCheck_ManagementApiDown_ReportsTheFailureInsteadOfThrowing()
    {
        var (result, _) = await CheckAsync(_ => throw new HttpRequestException("Connection refused"));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Connection refused", result.Description);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task QueueCheck_StatisticsNotYetAvailable_CountsAsNoConsumer()
    {
        var (result, _) = await CheckAsync(_ => Json("""{"name":"ms_vd_q_downloadRequest"}"""));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(0, result.Data["consumers"]);
    }

    private sealed class FixedCheck(HealthCheckResult result) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private static readonly HealthCheckResult OneConsumer = HealthCheckResult.Healthy(
        "1 Downloader consumer(s) on ms_vd_q_downloadRequest", new Dictionary<string, object> { ["consumers"] = 1 });

    private static async Task<(HealthCheckResult Result, StubHandler Handler)> WorkerCheckAsync(
        HealthCheckResult queue, Func<HttpRequestMessage, HttpResponseMessage> respond, string url = "http://downloader:8081/health")
    {
        var handler = new StubHandler(respond);
        var check = new DownloaderWorkerCheck(new FixedCheck(queue), new StubFactory(handler), url);
        var context = new HealthCheckContext { Registration = new HealthCheckRegistration("downloader", check, HealthStatus.Degraded, null) };
        return (await check.CheckHealthAsync(context), handler);
    }

    [Fact]
    public async Task WorkerCheck_BusyDownloader_IsHealthyAndSaysWhatItIsDoing()
    {
        var (result, handler) = await WorkerCheckAsync(OneConsumer, _ => Json("""
            {"status":"Healthy","description":"Busy for 3m on DOWNLOAD 42 (last progress 5s ago)","jobsSucceeded":7,
             "jobsFailed":1,"lastFailure":"HTTP Error 403","ytdlpVersion":"2026.10.26","job":{"type":"DOWNLOAD","managerId":42}}
            """));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Busy for 3m on DOWNLOAD 42 (last progress 5s ago)", result.Description);
        Assert.Equal(1, result.Data["consumers"]);
        Assert.Equal(7, result.Data["jobsSucceeded"]);
        Assert.Equal("HTTP Error 403", result.Data["lastFailure"]);
        Assert.Equal("http://downloader:8081/health", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task WorkerCheck_StuckDownloader_IsDegradedWithItsReason()
    {
        var (result, _) = await WorkerCheckAsync(OneConsumer, _ => Json(
            """{"status":"Unhealthy","description":"Stuck: DOWNLOAD 42 has shown no progress for 11m"}""",
            HttpStatusCode.ServiceUnavailable));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Stuck: DOWNLOAD 42 has shown no progress for 11m", result.Description);
    }

    [Fact]
    public async Task WorkerCheck_EndpointDown_IsDegraded()
    {
        var (result, _) = await WorkerCheckAsync(OneConsumer, _ => throw new HttpRequestException("Connection refused"));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("health endpoint doesn't answer: Connection refused", result.Description);
    }

    [Fact]
    public async Task WorkerCheck_NoConsumer_ReturnsTheQueueResultWithoutCallingTheEndpoint()
    {
        var noConsumer = HealthCheckResult.Degraded("No Downloader is consuming ms_vd_q_downloadRequest (2 message(s) waiting)");

        var (result, handler) = await WorkerCheckAsync(noConsumer, _ => throw new InvalidOperationException("must not be called"));

        Assert.Equal(noConsumer.Description, result.Description);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task WorkerCheck_WithoutAUrl_ChecksTheQueueOnly()
    {
        var (result, handler) = await WorkerCheckAsync(OneConsumer, _ => throw new InvalidOperationException("must not be called"), url: "");

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Null(handler.LastRequest);
    }

    private sealed class ListLogger : ILogger<HealthMetricsPublisher>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static HealthReport Report(HealthStatus database, HealthStatus downloader) => new(
        new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new(database, "db", TimeSpan.Zero, null, null),
            ["downloader"] = new(downloader, "no consumer", TimeSpan.Zero, null, null),
        },
        TimeSpan.Zero);

    [Fact]
    public async Task Publisher_RecordsEachCheckAndOverall_AndLogsOnlyChanges()
    {
        var logger = new ListLogger();
        var publisher = new HealthMetricsPublisher(logger);

        await publisher.PublishAsync(Report(HealthStatus.Healthy, HealthStatus.Healthy), default);
        await publisher.PublishAsync(Report(HealthStatus.Healthy, HealthStatus.Healthy), default);
        await publisher.PublishAsync(Report(HealthStatus.Healthy, HealthStatus.Degraded), default);

        Assert.Equal(HealthStatus.Degraded, publisher.Statuses["downloader"]);
        Assert.Equal(HealthStatus.Degraded, publisher.Statuses[HealthMetricsPublisher.Overall]);
        // First report: 3 "unknown -> Healthy" lines; second: none; third: downloader and overall changed.
        Assert.Equal(5, logger.Entries.Count);
        Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Warning));
        Assert.Contains(logger.Entries, e => e.Message.Contains("downloader is Degraded (was Healthy): no consumer"));
    }

    [Fact]
    public void EveryRegisteredCheck_HasATimeout_IncludingChecksRegisteredWithoutOne()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:QueueRequestDownload"] = "ms_vd_q_downloadRequest",
                ["AppSettings:ChannelHostName"] = "localhost",
            })
            .Build();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IAppSettings>(new AppSettings(configuration));
        services.AddDbContext<AppDbContext>();
        services.AddVdHealthChecks();
        // A check registered without a timeout, as MassTransit registers its bus check.
        services.AddHealthChecks().AddCheck("no-timeout", () => HealthCheckResult.Healthy());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        Assert.Contains(options.Registrations, r => r.Name == "database");
        Assert.All(options.Registrations, r => Assert.True(r.Timeout <= TimeSpan.FromSeconds(3), $"{r.Name} has timeout {r.Timeout}"));
    }

    [Fact]
    public async Task DatabaseCheck_TimedOut_SaysSoInPlainWords()
    {
        // No server needed: the token is already cancelled, as it is when the 3 s registration timeout fires.
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x").Options;
        await using var dbContext = new AppDbContext(dbOptions);
        var check = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", check, HealthStatus.Unhealthy, null, TimeSpan.FromSeconds(3)),
        };
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var result = await check.CheckHealthAsync(context, cancelled.Token);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("No answer from Postgres within 3 s", result.Description);
    }

    [Fact]
    public async Task ResponseWriter_WritesStatusAndEachCheck()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthSetup.WriteJsonAsync(context, Report(HealthStatus.Unhealthy, HealthStatus.Healthy));

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Unhealthy", json.RootElement.GetProperty("status").GetString());
        var database = json.RootElement.GetProperty("checks").EnumerateArray().First(c => c.GetProperty("name").GetString() == "database");
        Assert.Equal("Unhealthy", database.GetProperty("status").GetString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }
}
