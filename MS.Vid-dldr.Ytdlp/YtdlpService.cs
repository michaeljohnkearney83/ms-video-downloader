using System.Text.Json;
using MS.Vid_dldr.Ytdlp;
using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace MS.Vid_dldr.Ytdlp;

public class YtdlpService : IHostedService, IYtdlpService
{
    private static IAppSettings _appSettings;
    private FileSystemWatcher _fileSystemWatcher = null;
    private static HttpClient _httpClient = null;

    public YtdlpService(IAppSettings appSettings, IHttpClientFactory httpClientFactory)
    {
        _appSettings = appSettings;

        _httpClient = httpClientFactory.CreateClient("ms-vid-dldr-httpClient");

        var appSettingsValue = JsonSerializer.Serialize(_appSettings.ToString());

        WriteLog(Constantss.LogStatusInfo, _appSettings.ToString());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        WriteLog(Constantss.LogStatusCall, $"StartAsync(CancellationToken cancellationToken)");

        var timer = new System.Timers.Timer();
        timer.Interval = _appSettings.PollInterval;
        timer.Elapsed += OnTimedEvent;
        timer.AutoReset = true;
        timer.Enabled = true;

        return Task.CompletedTask;
    }

    private async static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
    {
        WriteLog(Constantss.LogStatusInfo, $"The Version check raised at {e.SignalTime}");

        // async void: CheckVersionAsync must never throw, or the exception would end the process.
        await CheckVersionAsync(() => new HtmlWeb().Load(_appSettings.YtdlpGitUrl));
    }

    /// <summary>Reads the latest yt-dlp version from the page and reports it to the Manager. Never throws.</summary>
    /// <returns>True when a version was found and the Manager accepted it.</returns>
    internal static async Task<bool> CheckVersionAsync(Func<HtmlDocument> loadPage)
    {
        string version;
        try
        {
            version = ParseVersion(loadPage(), _appSettings.QuerySelectorString, _appSettings.StringReplaceFoundVersion);
        }
        catch (Exception ex)
        {
            WriteLog(Constantss.LogStatusEx, $"{ex.ToString()}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            WriteLog(Constantss.LogStatusWarn, $"No yt-dlp version found with selector '{_appSettings.QuerySelectorString}' on {_appSettings.YtdlpGitUrl}; Manager not updated.");
            return false;
        }

        try
        {
            using var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
            var versionUpdateCall = _appSettings.ManagerApiEndpointUpdateVersion.Replace("{version}", Uri.EscapeDataString(version));
            using var response = await _httpClient.PostAsync(versionUpdateCall, httpContent);

            WriteLog(response.IsSuccessStatusCode ? Constantss.LogStatusInfo : Constantss.LogStatusErr,
                $"Reported yt-dlp version {version}: response.StatusCode: {response.StatusCode}");

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            WriteLog(Constantss.LogStatusEx, $"Reporting yt-dlp version {version} failed: {ex.ToString()}");
            return false;
        }
    }

    /// <summary>Returns the trimmed text of the first element matching <paramref name="querySelector"/>, minus <paramref name="prefixToRemove"/>, or "".</summary>
    internal static string ParseVersion(HtmlDocument? document, string querySelector, string? prefixToRemove)
    {
        var span = document?.DocumentNode.QuerySelector(querySelector);
        if (span == null) return "";

        var text = HtmlEntity.DeEntitize(span.InnerText).Trim();
        if (!string.IsNullOrEmpty(prefixToRemove)) text = text.Replace(prefixToRemove, "");

        return text.Trim();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        WriteLog(Constantss.LogStatusCall, $"StopAsync(CancellationToken cancellationToken)");

        if (_appSettings.CallManagerApiViaRest) _httpClient.Dispose();

        return Task.CompletedTask;
    }

    private static void WriteLog(string status, string line)
    {
        Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[{status}] {line}");
    }
}