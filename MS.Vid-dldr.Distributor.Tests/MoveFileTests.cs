using System.Net;

namespace MS.Vid_dldr.Distributor.Tests;

public class MoveFileTests
{
    private const string Id = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string DistributedPath = "/v1/distributor/downloadProgress/distributed/" + Id;
    private const string DeletedPathPrefix = "/v1/distributor/downloadProgress/deleted/" + Id + "/";

    private static string FileInfoJson(string folder, string name = "My video", string error = "") =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            requestedDownloadFolder = folder,
            requestedDownloadName = name,
            feedback = new { errorMessage = error },
        });

    private static (DistributorService service, FakeManagerHandler handler) CreateService(
        TempFolders folders, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeManagerHandler(respond);
        var settings = new TestAppSettings { DownloaderShareFilePath = folders.Share };
        return (new DistributorService(settings, new FakeHttpClientFactory(handler)), handler);
    }

    private static HttpResponseMessage Manager(HttpRequestMessage request, string destination,
        HttpStatusCode distributedStatus = HttpStatusCode.OK)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.StartsWith("/v1/distributor/downloadFileInfo/")) return FakeManagerHandler.Json(FileInfoJson(destination));
        if (path == DistributedPath) return FakeManagerHandler.Json("""{"feedback": {"errorMessage": ""}}""", distributedStatus);
        if (path.StartsWith(DeletedPathPrefix)) return FakeManagerHandler.Json("""{"feedback": {}}""");
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Moves_file_to_requested_folder_and_name_and_reports_distributed()
    {
        using var folders = new TempFolders();
        var source = folders.CreateShareFile(Id + ".mp4");
        var (service, handler) = CreateService(folders, r => Manager(r, folders.Destination));

        var result = await service.MoveFileAsync(source, "test");

        Assert.True(result);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(folders.Destination, "My video.mp4")));
        Assert.Contains(DistributedPath, handler.Paths);
    }

    [Fact]
    public async Task Unknown_client_id_leaves_file_in_share()
    {
        using var folders = new TempFolders();
        var source = folders.CreateShareFile(Id + ".mp4");
        var (service, _) = CreateService(folders, _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.False(await service.MoveFileAsync(source, "test"));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task Missing_deployment_folder_is_created_and_the_file_delivered()
    {
        using var folders = new TempFolders();
        var source = folders.CreateShareFile(Id + ".mp4");
        // The Manager's default folder for deployment 1380 on a machine that has never had one.
        var missing = Path.Combine(folders.Root, "vd", "1380") + Path.DirectorySeparatorChar;
        var (service, handler) = CreateService(folders, r => Manager(r, missing));

        Assert.True(await service.MoveFileAsync(source, "test"));
        Assert.False(File.Exists(source));
        Assert.Single(Directory.GetFiles(missing));
        Assert.Contains(DistributedPath, handler.Paths);
    }

    [Fact]
    public void A_deleted_share_folder_is_created_again()
    {
        using var folders = new TempFolders();
        var (service, _) = CreateService(folders, r => Manager(r, folders.Destination));
        Directory.Delete(folders.Share, recursive: true);

        service.CheckShareFolder();

        Assert.True(Directory.Exists(folders.Share));
    }

    [Fact]
    public async Task Deployment_folder_that_cannot_be_created_deletes_share_file_and_reports_deleted()
    {
        using var folders = new TempFolders();
        var source = folders.CreateShareFile(Id + ".mp4");
        var blocker = Path.Combine(folders.Root, "a-file");
        File.WriteAllText(blocker, "x");
        var (service, handler) = CreateService(folders, r => Manager(r, Path.Combine(blocker, "1380")));

        Assert.False(await service.MoveFileAsync(source, "test"));
        Assert.False(File.Exists(source));
        Assert.Contains(handler.Paths, p => p.StartsWith(DeletedPathPrefix));
    }

    [Fact]
    public async Task Failed_distributed_update_is_handled_without_an_exception()
    {
        using var folders = new TempFolders();
        var source = folders.CreateShareFile(Id + ".mp4");
        var (service, _) = CreateService(folders, r => Manager(r, folders.Destination, HttpStatusCode.InternalServerError));
        using var console = new ConsoleCapture();

        var result = await service.MoveFileAsync(source, "test");

        Assert.False(result);
        Assert.DoesNotContain($"[{Constantss.LogStatusEx}]", console.Output);
        Assert.Contains("CallUpdateDownloadProgressDistributedAsync returned: null", console.Output);
    }

    [Fact]
    public async Task Locked_file_retry_logs_show_actual_values()
    {
        using var folders = new TempFolders();
        var source = folders.CreateShareFile(Id + ".mp4");
        var (service, _) = CreateService(folders, r => Manager(r, folders.Destination));
        using var console = new ConsoleCapture();

        bool result;
        using (new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))   // blocks move and delete
        {
            result = await service.MoveFileAsync(source, "test");
        }

        Assert.False(result);
        Assert.Contains("DistributeFileToDeploymentFolder IOException retryCount: 2", console.Output);
        Assert.Contains("Failed to delete after 2 retries & waiting 0.", console.Output);
        Assert.DoesNotContain("{retryCount}", console.Output);
        Assert.DoesNotContain("{_appSettings", console.Output);
    }

    [Fact]
    public void Sanitise_lowercases_and_replaces_unsafe_characters()
    {
        using var folders = new TempFolders();
        var (service, _) = CreateService(folders, _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal("folder_doesnt_exist_c___x", service.Sanitise("Folder doesnt exist C:\\/X"));
        Assert.Equal("a-b_c", service.Sanitise("A-B   C"));
    }
}
