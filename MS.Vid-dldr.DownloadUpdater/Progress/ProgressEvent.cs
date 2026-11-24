using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.DownloadUpdater.Progress;

/// <summary>
/// What the Updater publishes on the Redis channel <see cref="RedisProgressStore.Channel"/> and stores in the hash
/// <c>vd:progress:{id}</c>. The Manager reads the same shape (Live/ProgressEvent.cs there); keep them in step.
/// </summary>
public sealed record ProgressEvent(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("percent")] string? Percent,
    [property: JsonPropertyName("speed")] string? Speed,
    [property: JsonPropertyName("eta")] string? Eta,
    [property: JsonPropertyName("updatedUtc")] DateTime UpdatedUtc)
{
    public string ToJson() => JsonSerializer.Serialize(this);
}
