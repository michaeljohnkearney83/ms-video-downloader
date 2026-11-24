namespace MS.Vid_dldr.Manager.Helpers;

/// <summary>
/// Which user actions (pause, resume, cancel) a download's status allows. The Manager owns the state: it sets PAUSED or
/// CANCELLED straight away, tells the Downloader to stop, and ignores late progress for that download.
/// </summary>
public static class DownloadActions
{
    public const string Pause = "PAUSE";
    public const string Resume = "RESUME";
    public const string Cancel = "CANCEL";

    /// <summary>Waiting for, or being worked on by, the Downloader.</summary>
    private static readonly HashSet<string> Active =
    [
        Constants.DownloadStatusTypeRequested,
        Constants.DownloadStatusTypeQueued,
        Constants.DownloadStatusTypeAnalysis,
        Constants.DownloadStatusTypeDownloading,
        Constants.DownloadStatusTypeRetry,
    ];

    public static bool CanPause(string status) => Active.Contains(status);

    public static bool CanResume(string status) => status == Constants.DownloadStatusTypePaused;

    public static bool CanCancel(string status) => Active.Contains(status) || status == Constants.DownloadStatusTypePaused;

    /// <summary>Progress, finished and errored updates for these are ignored: the user stopped the download.</summary>
    public static bool IsStoppedByUser(string status) =>
        status is Constants.DownloadStatusTypePaused or Constants.DownloadStatusTypeCancelled;

    /// <summary>
    /// A Distributor asking with username "*" may deliver anyone's downloads, but only where the Manager allows it
    /// (AppSettings:AllowAnyUserDistribution; single-user machines).
    /// </summary>
    public static bool IsAnyUserDistribution(string requestedUsername, bool allowAnyUser) =>
        allowAnyUser && requestedUsername == "*";

    /// <summary>Null when the action is allowed, otherwise the reason it isn't.</summary>
    public static string? Validate(string action, string status) => action switch
    {
        Pause when !CanPause(status) => $"Only a queued or running download can be paused; this one is {status}",
        Resume when !CanResume(status) => $"Only a paused download can be resumed; this one is {status}",
        Cancel when !CanCancel(status) => $"Only a queued, running or paused download can be cancelled; this one is {status}",
        Pause or Resume or Cancel => null,
        _ => $"Unknown action {action}",
    };
}
