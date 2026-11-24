namespace MS.Vid_dldr.Manager;

public interface IAppSettings
{
    string ConnectionStringsDownloadsDb { get; set; }
    string HomeDrivePath { get; set; }
    string ChannelHostName { get; set; }
    int ChannelPortNumber { get; set; }
    string ChannelUsername { get; set; }
    string ChannelPassword { get; set; }
    string QueueRequestDownload { get; set; }
    string QueueDownloadUpdate { get; set; }
    int ChannelManagementPort { get; set; }
    string DownloaderHealthUrl { get; set; }
    int WaitForYtdlpToFinishRetryMaxCount { get; set; }
    int WaitForYtdlpToFinishRetryWait { get; set; }
    int MaxFilepathLength { get; set; }
    int RetryOnErroredMaxCount { get; set; }
    int ExtractInfoFormatTimeoutMs { get; set; }
}