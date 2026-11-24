using System.Collections.Concurrent;

namespace MS.Vid_dldr.DownloadUpdater.Progress;

/// <summary>
/// Decides which DOWNLOADING ticks also go to the Manager (Postgres). Every tick goes to Redis; the database only needs
/// to know a download is running and roughly how far it got, so it gets:
/// <list type="bullet">
/// <item>the first tick of a download (QUEUED/ANALYSIS -> DOWNLOADING is a state change),</item>
/// <item>the first tick after a gap (a resume after a pause, or a stall that recovered),</item>
/// <item>otherwise one tick per <see cref="Interval"/> (a checkpoint, so the list is close if Redis is lost).</item>
/// </list>
/// State changes (FINISHED, ERRORED, ...) are not throttled; <see cref="Forget"/> them.
/// </summary>
public sealed class ProgressCheckpoint(TimeSpan interval, TimeSpan gap)
{
    private sealed record Entry(DateTime LastPersisted, DateTime LastSeen);

    private readonly ConcurrentDictionary<int, Entry> _entries = new();

    public TimeSpan Interval { get; } = interval;
    public TimeSpan Gap { get; } = gap;

    /// <summary>Records a DOWNLOADING tick for <paramref name="downloadId"/> and says whether to persist it.</summary>
    public bool ShouldPersist(int downloadId, DateTime nowUtc)
    {
        var persist = true;
        _entries.AddOrUpdate(downloadId,
            _ => new Entry(nowUtc, nowUtc),
            (_, e) =>
            {
                persist = nowUtc - e.LastSeen >= Gap || nowUtc - e.LastPersisted >= Interval;
                return new Entry(persist ? nowUtc : e.LastPersisted, nowUtc);
            });

        // Downloads that never sent FINISHED/ERRORED (worker crash) must not pile up.
        if (_entries.Count > 1000)
            foreach (var (id, e) in _entries)
                if (nowUtc - e.LastSeen > TimeSpan.FromHours(1)) _entries.TryRemove(id, out _);

        return persist;
    }

    public void Forget(int downloadId) => _entries.TryRemove(downloadId, out _);

    public int Count => _entries.Count;
}
