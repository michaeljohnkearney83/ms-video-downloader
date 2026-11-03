using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

public class FragmentDto
{
    [JsonPropertyName("Url")]
    public string Url { get; set; }

    [JsonPropertyName("Duration")]
    public double Duration { get; set; }
}