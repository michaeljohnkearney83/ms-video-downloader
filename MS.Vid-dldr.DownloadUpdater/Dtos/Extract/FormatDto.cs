using System.Text.Json;
using System.Text.Json.Serialization;
using MS.Vid_dldr.DownloadUpdater.Dtos;

namespace MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

public class FormatDto
{
    [JsonPropertyName("format_id")]
    public string FormatId { get; set; }

    [JsonPropertyName("format_note")]
    public string FormatNote { get; set; }

    [JsonPropertyName("ext")]
    public string Ext { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("acodec")]
    public string Acodec { get; set; }

    [JsonPropertyName("vcodec")]
    public string Vcodec { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    [JsonPropertyName("rows")]
    public int? Rows { get; set; }

    [JsonPropertyName("columns")]
    public int? Columns { get; set; }

    [JsonPropertyName("fragments")]
    public List<FragmentDto> Fragments { get; set; }

    [JsonPropertyName("audio_ext")]
    public string AudioExt { get; set; }

    [JsonPropertyName("video_ext")]
    public string VideoExt { get; set; }

    [JsonPropertyName("vbr")]
    public double? Vbr { get; set; }

    [JsonPropertyName("abr")]
    public double? Abr { get; set; }

    [JsonPropertyName("tbr")]
    public double? Tbr { get; set; }

    [JsonPropertyName("resolution")]
    public string Resolution { get; set; }

    [JsonPropertyName("aspect_ratio")]
    public double? AspectRatio { get; set; }

    [JsonPropertyName("filesize_approx")]
    public int? FilesizeApprox { get; set; }

    [JsonPropertyName("http_headers")]
    public HttpHeadersDto HttpHeaders { get; set; }

    [JsonPropertyName("format")]
    public string FormatTitle { get; set; }

    [JsonIgnore]
    //[JsonPropertyName("format_index")]
    public object FormatIndex { get; set; }

    [JsonPropertyName("manifest_url")]
    public string ManifestUrl { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }

    [JsonIgnore]
    //[JsonPropertyName("preference")]
    public object Preference { get; set; }

    [JsonPropertyName("quality")]
    public double? Quality { get; set; }

    [JsonPropertyName("has_drm")]
    public bool? HasDrm { get; set; }

    [JsonPropertyName("source_preference")]
    public int? SourcePreference { get; set; }

    [JsonPropertyName("asr")]
    public int? Asr { get; set; }

    [JsonPropertyName("filesize")]
    public int? Filesize { get; set; }

    [JsonPropertyName("audio_channels")]
    public int? AudioChannels { get; set; }

    [JsonPropertyName("language_preference")]
    public int? LanguagePreference { get; set; }

    [JsonPropertyName("dynamic_range")]
    public string DynamicRange { get; set; }

    [JsonPropertyName("container")]
    public string Container { get; set; }

    [JsonPropertyName("downloader_options")]
    public DownloaderOptionsDto DownloaderOptions { get; set; }
}