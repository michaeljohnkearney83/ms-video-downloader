using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

public class HttpHeadersDto
{
    [JsonPropertyName("Url")]
    public string Url { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}