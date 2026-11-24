using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Profiles;
using MS.Vid_dldr.Manager.Protos.FromClient;

namespace MS.Vid_dldr.Manager.Tests;

public class ServiceHelperTests
{
    private static ServiceHelper CreateHelper(FakeDownloadRequestPublisher? publisher = null, IMapper? mapper = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:ChannelHostName"] = "localhost",
                ["AppSettings:ChannelPortNumber"] = "5672",
                ["AppSettings:QueueRequestDownload"] = "ms_vd_q_downloadRequest",
            })
            .Build();

        // The DbContext is not used by SetupMethod or RetryAnalysisDownloadAsync.
        return new ServiceHelper(NullLogger<ServiceHelper>.Instance, mapper!, new AppSettings(configuration), null!,
            publisher ?? new FakeDownloadRequestPublisher());
    }

    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<DownloadProfiles>()).CreateMapper();

    private static Download CreateDownload(int id, string url)
    {
        var sessionId = Guid.NewGuid();
        var now = new DateTime(2026, 11, 23, 16, 0, 0, DateTimeKind.Utc);
        return new Download
        {
            Id = id,
            ClientId = Guid.NewGuid(),
            RequestedOn = now,
            RequestedUrl = url,
            RequestedDownloadFolder = @"E:\123\",
            RequestedDownloadName = "video",
            RequestedDownloadExt = "mp4",
            RequestedRetryCount = 3,
            RequestedTimeoutSeconds = 600,
            RequestedDownloadStalledTimeoutSeconds = 60,
            LastActivity = now,
            Status = Constants.DownloadStatusTypeErrored,
            RetryOnErroredAttemptNo = 1,
            RetryOnErroredMaxCount = 3,
            DownloadSessionId = sessionId,
            DownloadSession = new DownloadSession
            {
                Id = sessionId, Username = "user", Fid = "F1", ClientName = "client", DeploymentNo = "123",
                RequestedOn = now, LastActivity = now,
            },
        };
    }

    [Fact]
    public void SetupMethod_WithRequest_ReturnsResponseWithFeedbackContainingRequestJson()
    {
        var request = new RequestDownloadRequest { RequestedUrl = "https://example.com/video" };

        var response = CreateHelper().SetupMethod<RequestDownloadResponse>(request, "RequestDownload");

        Assert.NotNull(response.Feedback);
        Assert.Equal("", response.Feedback.ErrorMessage);
        Assert.Contains("https://example.com/video", response.Feedback.RequestJsonString);
    }

    [Fact]
    public void SetupMethod_NullRequest_SetsErrorMessage()
    {
        var response = CreateHelper().SetupMethod<RequestDownloadResponse>(null!);

        Assert.Equal("You must supply a valid request object", response.Feedback.ErrorMessage);
        Assert.Equal("MISSING", response.Feedback.RequestJsonString);
    }

    [Fact]
    public async Task RetryAnalysisDownloadAsync_SendsAnalysisRequestMarkedAsRetry()
    {
        var publisher = new FakeDownloadRequestPublisher();
        var download = CreateDownload(7, "https://example.com/v");

        var result = await CreateHelper(publisher, CreateMapper()).RetryAnalysisDownloadAsync(download);

        Assert.True(result);
        var sent = Assert.IsType<AnalysisRequestDto>(Assert.Single(publisher.Sent));
        Assert.True(sent.IsRetry);
        Assert.Equal(7, sent.Id);
        Assert.Equal("https://example.com/v", sent.RequestedUrl);
    }

    [Fact]
    public async Task RetryAnalysisDownloadAsync_SendFails_ReturnsFalse()
    {
        var publisher = new FakeDownloadRequestPublisher { ThrowOnSend = new InvalidOperationException("broker down") };

        var result = await CreateHelper(publisher, CreateMapper()).RetryAnalysisDownloadAsync(CreateDownload(1, "https://example.com"));

        Assert.False(result);
    }
}
