
namespace MS.Vid_dldr.Manager;

public static class Constants
{
    public const string MsNoticeLogPrefix = " >>--> ";
    public const string DownloadStatusTypeNone = "NONE";
    public const string DownloadStatusTypeRequested = "REQUESTED";
    public const string DownloadStatusTypeQueued = "QUEUED";
    public const string DownloadStatusTypeAnalysis = "ANALYSIS";
    public const string DownloadStatusTypeDownloading = "DOWNLOADING";
    public const string DownloadStatusTypeFinished = "FINISHED";
    public const string DownloadStatusTypeDistributedLookup = "DISTRIBUTED_LOOKUP";
    public const string DownloadStatusTypeDistributed = "DISTRIBUTED";
    public const string DownloadStatusTypeDeleted = "DELETED";
    public const string DownloadStatusTypeErrored = "ERRORED";
    public const string DownloadStatusTypeRetry = "RETRY";
    /// <summary>Stopped by the user; the partial file is kept and Resume continues it.</summary>
    public const string DownloadStatusTypePaused = "PAUSED";
    /// <summary>Stopped by the user; the partial file is deleted.</summary>
    public const string DownloadStatusTypeCancelled = "CANCELLED";
    public const string ExtractInfoTypeFormats = "FORMATS";
}