using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.Manager.Dtos;

public class AnalysisRequestDto : IMessageDto
{
    [JsonPropertyName("messageType")]
    public string MessageType { get; } = "ANALYSIS";
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
    [JsonPropertyName("isRetry")]
    public bool IsRetry { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}