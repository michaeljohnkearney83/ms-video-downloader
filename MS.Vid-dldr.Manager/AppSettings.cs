namespace MS.Vid_dldr.Manager;

public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        ConnectionStringsDownloadsDb = configuration["ConnectionStrings:DownloadsDb"];
        HomeDrivePath = configuration["AppSettings:HomeDrivePath"];
        ChannelHostName = configuration["AppSettings:ChannelHostName"];
        ChannelPortNumber = Convert.ToInt32(configuration["AppSettings:ChannelPortNumber"]);
        QueueRequestDownload = configuration["AppSettings:QueueRequestDownload"];
        WaitForYtdlpToFinishRetryMaxCount = Convert.ToInt32(configuration["AppSettings:WaitForYtdlpToFinishRetryMaxCount"]);
        WaitForYtdlpToFinishRetryWait = Convert.ToInt32(configuration["AppSettings:WaitForYtdlpToFinishRetryWait"]);
        MaxFilepathLength = Convert.ToInt32(configuration["AppSettings:MaxFilepathLength"]);
        RetryOnErroredMaxCount = Convert.ToInt32(configuration["AppSettings:RetryOnErroredMaxCount"]);
        ExtractInfoFormatTimeoutMs = Convert.ToInt32(configuration["AppSettings:ExtractInfoFormatTimeoutMs"]);
    }

    public string ConnectionStringsDownloadsDb { get; set; }
    public string HomeDrivePath { get; set; }
    public string ChannelHostName { get; set; }
    public int ChannelPortNumber { get; set; }
    public string QueueRequestDownload { get; set; }
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
        $"+ ChannelHostName: {ChannelHostName}, \r\n" +
        $"+ ChannelPortNumber: {ChannelPortNumber}, \r\n" +
        $"+ QueueRequestDownload: {QueueRequestDownload}, \r\n" +
        $"+ WaitForYtdlpToFinishRetryMaxCount: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ WaitForYtdlpToFinishRetryWait: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ MaxFilepathLength: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ RetryOnErroredMaxCount: {WaitForYtdlpToFinishRetryMaxCount}, \r\n" +
        $"+ ExtractInfoFormatTimeoutMs: {WaitForYtdlpToFinishRetryMaxCount}";
    }
}