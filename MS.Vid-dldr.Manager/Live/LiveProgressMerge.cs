namespace MS.Vid_dldr.Manager.Live;

/// <summary>
/// Postgres has a checkpoint every 30 s; Redis has the latest tick. Use Redis only while the database still says the
/// download is on its way, so a pause, cancel, finish or error saved in Postgres always wins over a stale hash.
/// </summary>
public static class LiveProgressMerge
{
    private static readonly HashSet<string> InFlight =
        [Constants.DownloadStatusTypeQueued, Constants.DownloadStatusTypeAnalysis, Constants.DownloadStatusTypeDownloading];

    public static (string Status, string? Percent) Merge(string dbStatus, string? dbPercent, LiveProgress? live) =>
        live is { Status: Constants.DownloadStatusTypeDownloading } && InFlight.Contains(dbStatus)
            ? (live.Status, string.IsNullOrWhiteSpace(live.Percent) ? dbPercent : live.Percent)
            : (dbStatus, dbPercent);
}
