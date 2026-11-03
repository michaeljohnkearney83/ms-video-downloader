using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.DownloadUpdater.Dtos;

public class SetVersionsDto
{
    [JsonPropertyName("managerApp")]
    public string ManagerApp { get; set; }
    [JsonPropertyName("managerFramework")]
    public string ManagerFramework { get; set; }
    [JsonPropertyName("managerDb")]
    public string ManagerDb { get; set; }
    [JsonPropertyName("managerDbLastMigration")]
    public string ManagerDbLastMigration { get; set; }
    [JsonPropertyName("downloaderApp")]
    public string DownloaderApp { get; set; }
    [JsonPropertyName("downloaderFramework")]
    public string DownloaderFramework { get; set; }
    [JsonPropertyName("downloaderYtdlp")]
    public string DownloaderYtdlp { get; set; }
    [JsonPropertyName("updaterApp")]
    public string UpdaterApp { get; set; }
    [JsonPropertyName("updaterFramework")]
    public string UpdaterFramework { get; set; }
    [JsonPropertyName("requested")]
    public string Requested { get; set; } = DateTime.UtcNow.ToString();

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}