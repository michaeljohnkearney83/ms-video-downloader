namespace MS.Vid_dldr.Manager;

public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        ConnectionStringsDownloadsDb = configuration["ConnectionStrings:DownloadsDb"];
        HomeDrivePath = configuration["AppSettings:HomeDrivePath"];
        HomeDriveMountPath = configuration["AppSettings:HomeDriveMountPath"] ?? "";
        ChannelHostName = configuration["AppSettings:ChannelHostName"];
        ChannelPortNumber = Convert.ToInt32(configuration["AppSettings:ChannelPortNumber"]);
        ChannelUsername = configuration["AppSettings:ChannelUsername"] ?? "guest";
        ChannelPassword = configuration["AppSettings:ChannelPassword"] ?? "guest";
        QueueRequestDownload = configuration["AppSettings:QueueRequestDownload"];
        QueueDownloadUpdate = configuration["AppSettings:QueueDownloadUpdate"] ?? "ms_vd_q_downloadUpdate";
        ChannelManagementPort = int.TryParse(configuration["AppSettings:ChannelManagementPort"], out var managementPort)
            ? managementPort : 15672;
        DownloaderHealthUrl = configuration["AppSettings:DownloaderHealthUrl"] ?? "";
        ExchangeDownloadControl = configuration["AppSettings:ExchangeDownloadControl"] ?? "ms_vd_x_downloadControl";
        AllowAnyUserDistribution = bool.TryParse(configuration["AppSettings:AllowAnyUserDistribution"], out var anyUser) && anyUser;
        WaitForYtdlpToFinishRetryMaxCount = Convert.ToInt32(configuration["AppSettings:WaitForYtdlpToFinishRetryMaxCount"]);
        WaitForYtdlpToFinishRetryWait = Convert.ToInt32(configuration["AppSettings:WaitForYtdlpToFinishRetryWait"]);
        MaxFilepathLength = Convert.ToInt32(configuration["AppSettings:MaxFilepathLength"]);
        RetryOnErroredMaxCount = Convert.ToInt32(configuration["AppSettings:RetryOnErroredMaxCount"]);
        ExtractInfoFormatTimeoutMs = Convert.ToInt32(configuration["AppSettings:ExtractInfoFormatTimeoutMs"]);
    }

    public string ConnectionStringsDownloadsDb { get; set; }
    public string HomeDrivePath { get; set; }
    /// <summary>
    /// HomeDrivePath as mounted into the Manager's container (e.g. /home-drive), so a session's deployment folder can be
    /// created when the session starts. Empty = don't create it here (the Distributor creates it on delivery).
    /// </summary>
    public string HomeDriveMountPath { get; set; }
    public string ChannelHostName { get; set; }
    public int ChannelPortNumber { get; set; }
    public string ChannelUsername { get; set; }
    public string ChannelPassword { get; set; }
    public string QueueRequestDownload { get; set; }
    /// <summary>Queue the DownloadUpdater consumes (the health check looks for its consumer).</summary>
    public string QueueDownloadUpdate { get; set; }
    /// <summary>RabbitMQ management API port, used by the health checks to count queue consumers.</summary>
    public int ChannelManagementPort { get; set; }
    /// <summary>The Python Downloader's /health (e.g. http://downloader:8081/health). Empty = queue check only.</summary>
    public string DownloaderHealthUrl { get; set; }
    /// <summary>Fanout exchange for pause / cancel messages to every Downloader.</summary>
    public string ExchangeDownloadControl { get; set; }
    /// <summary>
    /// Lets a Distributor ask for any user's downloads (username "*"). Off by default: in production several users
    /// share the network folder and each Distributor delivers only its own user's files. On in the Dev compose.
    /// </summary>
    public bool AllowAnyUserDistribution { get; set; }
    public int WaitForYtdlpToFinishRetryMaxCount { get; set; }
    public int WaitForYtdlpToFinishRetryWait { get; set; }
    public int MaxFilepathLength { get; set; }
    public int RetryOnErroredMaxCount { get; set; }
    public int ExtractInfoFormatTimeoutMs { get; set; }

    public override string ToString()
    {
        return
        $"+ ConnectionStringsDownloadsDb: {ConnectionStringsDownloadsDb}, \r\n" +
        $"+ HomeDrivePath: {HomeDrivePath}, \r\n" +
        $"+ HomeDriveMountPath: {HomeDriveMountPath}, \r\n" +
        $"+ ChannelHostName: {ChannelHostName}, \r\n" +
        $"+ ChannelPortNumber: {ChannelPortNumber}, \r\n" +
        $"+ ChannelUsername: {ChannelUsername}, \r\n" +
        $"+ QueueRequestDownload: {QueueRequestDownload}, \r\n" +
        $"+ WaitForYtdlpToFinishRetryMaxCount: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ WaitForYtdlpToFinishRetryWait: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ MaxFilepathLength: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ RetryOnErroredMaxCount: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ ExtractInfoFormatTimeoutMs: {WaitForYtdlpToFinishRetryMaxCount}";
    }
}