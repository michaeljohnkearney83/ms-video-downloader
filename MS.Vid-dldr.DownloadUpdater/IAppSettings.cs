namespace MS.Vid_dldr.DownloadUpdater;

public interface IAppSettings
{
    string ManagerApiAddress { get; set; }
    string ChannelHostName { get; set; }
    int ChannelPortNumber { get; set; }
    string QueueRequestDownload { get; set; }
    string QueueDownloadUpdate { get; set; }
    int ProgressCheckpointSeconds { get; set; }
}