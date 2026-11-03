using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

public class DownloaderOptionsDto
{
    [JsonPropertyName("http_chunk_size")]
    public int? HttpChunkSize { get; set; }
}