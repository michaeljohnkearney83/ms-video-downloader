using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.Manager.Dtos;

public class DownloadRequestDto : IMessageDto
{
    [JsonPropertyName("messageType")]
    public string MessageType { get; } = "DOWNLOAD";
    [JsonPropertyName("managerId")]
    public int Id { get; set; }
    [JsonPropertyName("downloadId")]
    public Guid ClientId { get; set; }
    [JsonPropertyName("url")]
    public string RequestedUrl { get; set; }
    [JsonPropertyName("downloadName")]
    public string RequestedDownloadName { get; set; }
    [JsonPropertyName("downloadExt")]
    public string RequestedDownloadExt { get; set; }
    [JsonPropertyName("retryCount")]
    public string RequestedRetryCount { get; set; }
    [JsonPropertyName("timeoutSeconds")]
    public string RequestedTimeoutSeconds { get; set; }
    [JsonPropertyName("downloadStalledTimeoutSeconds")]
    public string RequestedDownloadStalledTimeoutSeconds { get; set; }

    /// <summary>True when resuming a paused download: the Downloader continues the partial file.</summary>
    [JsonPropertyName("resume")]
    public bool Resume { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}