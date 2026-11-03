using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;

// YtdlpService keeps its settings and HttpClient in static fields, so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MS.Vid_dldr.Ytdlp.Tests;

public class YtdlpServiceTests
{
    private const string Selector = "span.css-truncate.css-truncate-target.text-bold.mr-2";

    // Trimmed copy of the "Releases" sidebar on https://github.com/yt-dlp/yt-dlp.
    private const string ReleasePage = """
        <html><body>
          <a href="/yt-dlp/yt-dlp/releases/tag/2025.01.15">
            <span class="css-truncate css-truncate-target text-bold mr-2" style="max-width: none;">
              yt-dlp 2025.01.15
            </span>
            <span class="Label Label--success">Latest</span>
          </a>
        </body></html>
        """;

    private static HtmlDocument Html(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["BuildType"] = "DEBUG",
        ["ManagerApiRestConfig:BaseUrl"] = "http://manager.test",
        ["ManagerApiRestConfig:Timeout"] = "00:00:30",
        ["CallManagerApiViaRest"] = "true",
        ["YtdlpGitUrl"] = "https://github.com/yt-dlp/yt-dlp",
        ["QuerySelectorString"] = Selector,
        ["StringReplaceFoundVersion"] = "yt-dlp ",
        ["PollInterval"] = "30000",
        ["ManagerApiEndpointUpdateVersion"] = "/v1/ytdlp/version/{version}",
    }).Build();

    private static FakeManagerHandler CreateService(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeManagerHandler(respond);
        _ = new YtdlpService(new AppSettings(Config()), new FakeHttpClientFactory(handler));
        return handler;
    }

    [Fact]
    public void ParseVersion_reads_release_span_and_strips_prefix_and_whitespace()
    {
        Assert.Equal("2025.01.15", YtdlpService.ParseVersion(Html(ReleasePage), Selector, "yt-dlp "));
    }

    [Fact]
    public void ParseVersion_without_prefix_setting_returns_trimmed_text()
    {
        Assert.Equal("yt-dlp 2025.01.15", YtdlpService.ParseVersion(Html(ReleasePage), Selector, null));
    }

    [Theory]
    [InlineData("<html><body><span class=\"other\">yt-dlp 2025.01.15</span></body></html>")]   // markup changed
    [InlineData("<html><body><span class=\"css-truncate css-truncate-target text-bold mr-2\">  </span></body></html>")]
    public void ParseVersion_returns_empty_when_not_found(string html)
    {
        Assert.Equal("", YtdlpService.ParseVersion(Html(html), Selector, "yt-dlp "));
    }

    [Fact]
    public void ParseVersion_null_document_returns_empty()
    {
        Assert.Equal("", YtdlpService.ParseVersion(null, Selector, "yt-dlp "));
    }

    [Fact]
    public async Task CheckVersion_posts_found_version_to_manager()
    {
        var handler = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        Assert.True(await YtdlpService.CheckVersionAsync(() => Html(ReleasePage)));
        Assert.Equal(new[] { "POST /v1/ytdlp/version/2025.01.15" }, handler.Requests);
    }

    [Fact]
    public async Task CheckVersion_does_not_post_when_no_version_found()
    {
        var handler = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        Assert.False(await YtdlpService.CheckVersionAsync(() => Html("<html><body></body></html>")));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckVersion_does_not_post_when_page_load_fails()
    {
        var handler = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        Assert.False(await YtdlpService.CheckVersionAsync(() => throw new HttpRequestException("github unreachable")));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckVersion_does_not_throw_when_manager_is_unreachable()
    {
        CreateService(_ => throw new HttpRequestException("connection refused"));

        Assert.False(await YtdlpService.CheckVersionAsync(() => Html(ReleasePage)));
    }

    [Fact]
    public async Task CheckVersion_returns_false_when_manager_rejects()
    {
        CreateService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Assert.False(await YtdlpService.CheckVersionAsync(() => Html(ReleasePage)));
    }

    [Fact]
    public void AppSettings_loads_values_from_configuration()
    {
        var settings = new AppSettings(Config());

        Assert.Equal(new Uri("http://manager.test"), settings.ManagerApiAddressRestConfigBaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.ManagerApiAddressRestConfigTimeout);
        Assert.True(settings.CallManagerApiViaRest);
        Assert.Equal(30000, settings.PollInterval);
        Assert.Equal(Selector, settings.QuerySelectorString);
    }
}

internal sealed class FakeManagerHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeManagerHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<string> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
        return Task.FromResult(_respond(request));
    }
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) =>
        new(_handler, disposeHandler: false) { BaseAddress = new Uri("http://manager.test") };
}
