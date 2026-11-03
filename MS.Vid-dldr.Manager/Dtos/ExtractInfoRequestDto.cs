using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.Manager.Dtos;

public class ExtractInfoRequestDto : IMessageDto
{
    [JsonPropertyName("messageType")]
    public string MessageType { get; } = "EXTRACT_INFO";
    [JsonPropertyName("managerId")]
    public int Id { get; set; }
    [JsonPropertyName("url")]
    public string RequestedUrl { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}