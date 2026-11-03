using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

public class FragmentDto
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }
}