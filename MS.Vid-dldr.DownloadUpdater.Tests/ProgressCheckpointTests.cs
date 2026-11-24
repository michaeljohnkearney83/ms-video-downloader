using MS.Vid_dldr.DownloadUpdater.Progress;

namespace MS.Vid_dldr.DownloadUpdater.Tests;

public class ProgressCheckpointTests
{
    private static readonly DateTime T0 = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
    private static ProgressCheckpoint Checkpoint() => new(TimeSpan.FromSeconds(30), gap: TimeSpan.FromSeconds(10));

    [Fact]
    public void First_tick_is_persisted_then_one_per_interval()
    {
        var checkpoint = Checkpoint();
        // One tick a second for a minute: Postgres gets 0 s, 30 s and 60 s, i.e. 3 writes instead of 61.
        var persisted = Enumerable.Range(0, 61).Count(s => checkpoint.ShouldPersist(1, T0.AddSeconds(s)));

        Assert.Equal(3, persisted);
    }

    [Fact]
    public void A_tick_after_a_gap_is_persisted()
    {
        var checkpoint = Checkpoint();
        Assert.True(checkpoint.ShouldPersist(1, T0));
        Assert.False(checkpoint.ShouldPersist(1, T0.AddSeconds(5)));

        // Paused for 20 s, then resumed: the Manager has it as QUEUED, so the first tick must reach it.
        Assert.True(checkpoint.ShouldPersist(1, T0.AddSeconds(25)));
    }

    [Fact]
    public void Downloads_are_throttled_separately()
    {
        var checkpoint = Checkpoint();
        Assert.True(checkpoint.ShouldPersist(1, T0));
        Assert.True(checkpoint.ShouldPersist(2, T0.AddSeconds(1)));
        Assert.False(checkpoint.ShouldPersist(1, T0.AddSeconds(2)));
    }

    [Fact]
    public void A_forgotten_download_starts_again()
    {
        var checkpoint = Checkpoint();
        checkpoint.ShouldPersist(1, T0);
        checkpoint.Forget(1);

        Assert.Equal(0, checkpoint.Count);
        Assert.True(checkpoint.ShouldPersist(1, T0.AddSeconds(1)));
    }

    [Fact]
    public void Progress_event_has_the_fields_the_Manager_reads()
    {
        var json = new ProgressEvent(15, "DOWNLOADING", " 42.5%", "1.2MiB/s", "00:10", T0).ToJson();

        Assert.Contains("\"id\":15", json);
        Assert.Contains("\"status\":\"DOWNLOADING\"", json);
        Assert.Contains("\"percent\":\" 42.5%\"", json);
        Assert.Contains("\"updatedUtc\":", json);
    }
}
