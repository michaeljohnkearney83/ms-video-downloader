using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.Manager.Dtos;

/// <summary>
/// Tells every Downloader to stop a download (published to the fanout exchange AppSettings:ExchangeDownloadControl).
/// The one running it stops at its next progress hook; one that has it queued skips it.
/// </summary>
public class DownloadControlDto : IMessageDto
{
    [JsonPropertyName("messageType")]
    public string MessageType { get; } = "CONTROL";
    /// <summary>PAUSE (keep the partial file) or CANCEL (delete it).</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";
    [JsonPropertyName("managerId")]
    public int Id { get; set; }
    [JsonPropertyName("downloadId")]
    public Guid ClientId { get; set; }

    public override string ToString() => JsonSerializer.Serialize(this);
}
