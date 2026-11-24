using System.Text.Json.Serialization;

namespace MS.Vid_dldr.Manager.Live;

/// <summary>
/// A message on the Redis channel <see cref="LiveProgressSetup.Channel"/>, published by the DownloadUpdater
/// (Progress/ProgressEvent.cs there; keep them in step). DOWNLOADING = a progress tick; anything else = a state change.
/// </summary>
public sealed record ProgressEvent(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("percent")] string? Percent,
    [property: JsonPropertyName("speed")] string? Speed,
    [property: JsonPropertyName("eta")] string? Eta,
    [property: JsonPropertyName("updatedUtc")] DateTime UpdatedUtc);

/// <summary>What browsers receive: "progress" for a tick, "changed" when they should re-read the list.</summary>
public sealed record LiveDownloadMessage(string ClientId, string Status, string? Percent, string? Speed, string? Eta);

/// <summary>The live values Redis holds for a running download.</summary>
public sealed record LiveProgress(string Status, string? Percent);
