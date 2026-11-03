using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Protos.FromClient;

namespace MS.Vid_dldr.Manager.Tests;

public class ServiceHelperTests
{
    private static ServiceHelper CreateHelper()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:ChannelHostName"] = "localhost",
                ["AppSettings:ChannelPortNumber"] = "5672",
                ["AppSettings:QueueRequestDownload"] = "ms_vd_q_downloadRequest",
            })
            .Build();

        // The mapper and DbContext are not used by SetupMethod. RMQProducer does not connect until Publish.
        return new ServiceHelper(NullLogger<ServiceHelper>.Instance, null!, new AppSettings(configuration), null!);
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
}
