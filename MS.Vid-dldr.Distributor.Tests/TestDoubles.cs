using System.Net;
using System.Text;

// DistributorService keeps its settings and HttpClient in static fields and the tests capture Console output,
// so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MS.Vid_dldr.Distributor.Tests;

internal sealed class TestAppSettings : IAppSettings
{
    public string BuildType { get; set; } = "DEBUG";
    public string ManagerApiAddressGrpc { get; set; } = "http://localhost:5000";
    public Uri ManagerApiAddressRestConfigBaseUrl { get; set; } = new("http://manager.test");
    public TimeSpan ManagerApiAddressRestConfigTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool CallManagerApiViaRest { get; set; } = true;
    public string DownloaderShareFilePath { get; set; } = "";
    public string PartFileExt { get; set; } = ".part";
    public string TempMp4FileExt { get; set; } = ".temp.mp4";
    public string M4aFileExt { get; set; } = ".m4a";
    public string YtdlpFileExt { get; set; } = ".ytdlp";
    public string InfoFileExt { get; set; } = ".json";
    public List<string> FormatTypeWhitelist { get; set; } = new() { "f140.m4a", "f251.webm" };
    public string FragPartFileContains { get; set; } = ".part-Frag";
    public int DistributeFileToDeploymentFolderMaxRetry { get; set; } = 2;
    public int DistributeFileToDeploymentFolderWait { get; set; } = 0;
    public string HomeDriveLocation { get; set; } = "";
    public string DistributorUsername { get; set; } = "tester";
    public string ManagerApiEndpointGetRequestedDownloadFileInformation { get; set; } = "/v1/distributor/downloadFileInfo/{username}/{client_id}";
    public string ManagerApiEndpointUpdateDownloadProgressDistributed { get; set; } = "/v1/distributor/downloadProgress/distributed/{client_id}";
    public string ManagerApiEndpointUpdateDownloadProgressDeleted { get; set; } = "/v1/distributor/downloadProgress/deleted/{client_id}/{reason}";

    public override string ToString() => nameof(TestAppSettings);
}

/// <summary>Answers Manager REST calls from a callback and records every request path.</summary>
internal sealed class FakeManagerHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeManagerHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<string> Paths { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Paths.Add(request.RequestUri!.AbsolutePath);
        return Task.FromResult(_respond(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) =>
        new(_handler, disposeHandler: false) { BaseAddress = new Uri("http://manager.test") };
}

/// <summary>Redirects Console output for the lifetime of the object.</summary>
internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _original = Console.Out;
    private readonly StringWriter _writer = new();

    public ConsoleCapture() => Console.SetOut(_writer);

    public string Output => _writer.ToString();

    public void Dispose() => Console.SetOut(_original);
}

/// <summary>A share folder and a destination folder under %TEMP%, deleted afterwards.</summary>
internal sealed class TempFolders : IDisposable
{
    public TempFolders()
    {
        Root = Path.Combine(Path.GetTempPath(), "ms-vid-dldr-tests", Guid.NewGuid().ToString("N"));
        Share = Directory.CreateDirectory(Path.Combine(Root, "share")).FullName;
        Destination = Directory.CreateDirectory(Path.Combine(Root, "dest")).FullName;
    }

    public string Root { get; }
    public string Share { get; }
    public string Destination { get; }

    public string CreateShareFile(string name)
    {
        var path = Path.Combine(Share, name);
        File.WriteAllText(path, "video");
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}
