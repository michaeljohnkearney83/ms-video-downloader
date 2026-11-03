using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.Manager.Dtos.Extract;

public class DownloaderOptionsDto
{
    [JsonPropertyName("HttpChunkSize")]
    public int HttpChunkSize { get; set; }
}