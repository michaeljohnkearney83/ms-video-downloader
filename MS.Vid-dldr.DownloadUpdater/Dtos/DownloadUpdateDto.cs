using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.DownloadUpdater.Dtos;

public class DownloadUpdateDto
{
    [JsonPropertyName("managerId")]
    public int Id { get; set; }
    [JsonPropertyName("videoId")]
    public string VideoId { get; set; }
    [JsonPropertyName("videoTitle")]
    public string VideoTitle { get; set; }
    [JsonPropertyName("videoDescription")]
    public string VideoDescription { get; set; }
    [JsonPropertyName("videoDuration")]
    public string VideoDuration { get; set; }
    [JsonPropertyName("videoThumbnail")]
    public string VideoThumbnail { get; set; }
    [JsonPropertyName("videoUrl")]
    public string VideoUrl { get; set; }
    [JsonPropertyName("videoResolution")]
    public string VideoResolution { get; set; }
    [JsonPropertyName("videoAspectRatio")]
    public string VideoAspectRatio { get; set; }
    [JsonPropertyName("videoFilesizeApprox")]
    public string VideoFilesizeApprox { get; set; }
    [JsonPropertyName("activity")]
    public string Activity { get; set; }
    [JsonIgnore]
    public DateTime LastActivityUtc
    {
        get
        {
            return DateTime.Parse(Activity, new System.Globalization.CultureInfo("en-GB")).ToUniversalTime(); //DateTime.ParseExact(Activity, "dd-MM-yyyy hh:mm:ss", CultureInfo.InvariantCulture).ToUniversalTime();
        }
    }
    [JsonPropertyName("status")]
    public string Status { get; set; }
    [JsonPropertyName("filename")]
    public string Filename { get; set; }
    [JsonPropertyName("eta")]
    public string? ProgressEta { get; set; }
    [JsonPropertyName("speed")]
    public string? ProgressSpeed { get; set; }
    [JsonPropertyName("percent")]
    public string? ProgressPercent { get; set; }
    [JsonPropertyName("elapsed")]
    public string? ProgressElapsed { get; set; }
    [JsonPropertyName("default_template")]
    public string? ProgressDefaultTemplate { get; set; }
    [JsonPropertyName("errorMsg")]
    public string ErrorMessage { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}