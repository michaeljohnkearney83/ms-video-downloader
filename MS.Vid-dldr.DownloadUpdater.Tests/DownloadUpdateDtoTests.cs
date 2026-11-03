using System.Text.Json;
using MS.Vid_dldr.DownloadUpdater.Dtos;

namespace MS.Vid_dldr.DownloadUpdater.Tests;

public class DownloadUpdateDtoTests
{
    // Shape of the message the Python downloader publishes (main.py progressHooks); "activity" comes from
    // helper.getDateTimeNowHuman() -> "%d/%m/%Y %H:%M:%S" in the downloader's local time.
    private const string DownloadingMessage = """
        {"managerId": 42, "status": "DOWNLOADING", "activity": "03/11/2026 21:05:09",
         "filename": "3f2504e0.mp4", "eta": "00:10", "speed": "1.2MiB/s", "percent": " 42.0%",
         "elapsed": "00:05", "default_template": "[download] 42.0%"}
        """;

    [Fact]
    public void Downloader_progress_message_deserialises()
    {
        var dto = JsonSerializer.Deserialize<DownloadUpdateDto>(DownloadingMessage)!;

        Assert.Equal(42, dto.Id);
        Assert.Equal(Constantss.DownloadStatusTypeDownloading, dto.Status);
        Assert.Equal("3f2504e0.mp4", dto.Filename);
        Assert.Equal(" 42.0%", dto.ProgressPercent);
        Assert.Equal("1.2MiB/s", dto.ProgressSpeed);
        Assert.Equal("[download] 42.0%", dto.ProgressDefaultTemplate);
    }

    [Fact]
    public void Activity_is_read_as_day_month_year_local_time()
    {
        var dto = JsonSerializer.Deserialize<DownloadUpdateDto>(DownloadingMessage)!;

        var expected = new DateTime(2026, 11, 3, 21, 5, 9, DateTimeKind.Local).ToUniversalTime();
        Assert.Equal(expected, dto.LastActivityUtc);
        Assert.Equal(DateTimeKind.Utc, dto.LastActivityUtc.Kind);
    }
}
