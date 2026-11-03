namespace MS.Vid_dldr.Manager;

public interface IAppSettings
{
    string ConnectionStringsDownloadsDb { get; set; }
    string HomeDrivePath { get; set; }
    string ChannelHostName { get; set; }
    int ChannelPortNumber { get; set; }
    string QueueRequestDownload { get; set; }
    int WaitForYtdlpToFinishRetryMaxCount { get; set; }
    int WaitForYtdlpToFinishRetryWait { get; set; }
    int MaxFilepathLength { get; set; }
    int RetryOnErroredMaxCount { get; set; }
    int ExtractInfoFormatTimeoutMs { get; set; }
}