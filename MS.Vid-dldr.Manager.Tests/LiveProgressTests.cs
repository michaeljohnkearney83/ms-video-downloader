using System.Text.Json;
using MS.Vid_dldr.Manager.Live;

namespace MS.Vid_dldr.Manager.Tests;

public class LiveProgressTests
{
    private static readonly LiveProgress Tick = new("DOWNLOADING", " 42.5%");

    [Theory]
    [InlineData("DOWNLOADING")]
    [InlineData("QUEUED")] // resumed: Redis has the first ticks before the 30 s checkpoint
    [InlineData("ANALYSIS")]
    public void Running_download_shows_the_latest_tick(string dbStatus)
    {
        Assert.Equal(("DOWNLOADING", " 42.5%"), LiveProgressMerge.Merge(dbStatus, " 10.0%", Tick));
    }

    [Theory]
    [InlineData("PAUSED")]
    [InlineData("CANCELLED")]
    [InlineData("FINISHED")]
    [InlineData("ERRORED")]
    public void A_state_saved_in_Postgres_beats_a_stale_tick(string dbStatus)
    {
        Assert.Equal((dbStatus, " 10.0%"), LiveProgressMerge.Merge(dbStatus, " 10.0%", Tick));
    }

    [Fact]
    public void Without_Redis_the_checkpoint_is_shown()
    {
        Assert.Equal(("DOWNLOADING", " 10.0%"), LiveProgressMerge.Merge("DOWNLOADING", " 10.0%", null));
        Assert.Equal(("DOWNLOADING", " 10.0%"), LiveProgressMerge.Merge("DOWNLOADING", " 10.0%", new LiveProgress("DOWNLOADING", "")));
    }

    [Fact]
    public void Reads_the_Updaters_progress_message()
    {
        var progress = JsonSerializer.Deserialize<ProgressEvent>(
            """{"id":15,"status":"DOWNLOADING","percent":" 42.5%","speed":"1.2MiB/s","eta":"00:10","updatedUtc":"2026-11-24T12:00:00Z"}""")!;

        Assert.Equal(15, progress.Id);
        Assert.Equal(" 42.5%", progress.Percent);
        Assert.Equal("00:10", progress.Eta);
    }

    [Fact]
    public void Session_groups_ignore_guid_case()
    {
        Assert.Equal(DownloadsHub.Group("A1B2C3D4-E5F6-4789-ABCD-EF0123456789"),
            DownloadsHub.Group("a1b2c3d4-e5f6-4789-abcd-ef0123456789"));
    }
}
