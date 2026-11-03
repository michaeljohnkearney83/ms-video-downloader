using System.Text.Json;
using System.Text.Json.Serialization;
using MS.Vid_dldr.DownloadUpdater.Dtos;
using MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

namespace MS.Vid_dldr.Manager.Dtos.Extract;

public class FormatDto
{
    [JsonPropertyName("FormatId")]
    public string FormatId { get; set; }

    [JsonPropertyName("FormatNote")]
    public string FormatNote { get; set; }

    [JsonPropertyName("Ext")]
    public string Ext { get; set; }

    [JsonPropertyName("Protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("Acodec")]
    public string Acodec { get; set; }

    [JsonPropertyName("Vcodec")]
    public string Vcodec { get; set; }

    [JsonPropertyName("Url")]
    public string Url { get; set; }

    [JsonPropertyName("Width")]
    public int Width { get; set; }

    [JsonPropertyName("Height")]
    public int Height { get; set; }

    [JsonPropertyName("Fps")]
    public double Fps { get; set; }

    [JsonPropertyName("Rows")]
    public int Rows { get; set; }

    [JsonPropertyName("Columns")]
    public int Columns { get; set; }

    [JsonPropertyName("Fragments")]
    public List<FragmentDto> Fragments { get; set; }

    [JsonPropertyName("AudioExt")]
    public string AudioExt { get; set; }

    [JsonPropertyName("VideoExt")]
    public string VideoExt { get; set; }

    [JsonPropertyName("Vbr")]
    public double Vbr { get; set; }

    [JsonPropertyName("Abr")]
    public double Abr { get; set; }

    [JsonPropertyName("Tbr")]
    public double Tbr { get; set; }

    [JsonPropertyName("Resolution")]
    public string Resolution { get; set; }

    [JsonPropertyName("AspectRatio")]
    public double AspectRatio { get; set; }

    [JsonPropertyName("FilesizeApprox")]
    public int FilesizeApprox { get; set; }

    [JsonPropertyName("HttpHeaders")]
    public HttpHeadersDto HttpHeaders { get; set; }

    [JsonPropertyName("FormatTitle")]
    public string FormatTitle { get; set; }

    [JsonIgnore]
    //[JsonPropertyName("FormatIndex")]
    public object FormatIndex { get; set; }

    [JsonPropertyName("ManifestUrl")]
    public string ManifestUrl { get; set; }

    [JsonPropertyName("Language")]
    public string Language { get; set; }

    [JsonIgnore]
    //[JsonPropertyName("Preference")]
    public object Preference { get; set; }

    [JsonPropertyName("Quality")]
    public double Quality { get; set; }

    [JsonPropertyName("HasDrm")]
    public int HasDrm { get; set; }

    [JsonPropertyName("SourcePreference")]
    public int SourcePreference { get; set; }

    [JsonPropertyName("Asr")]
    public int Asr { get; set; }

    [JsonPropertyName("Filesize")]
    public int Filesize { get; set; }

    [JsonPropertyName("AudioChannels")]
    public int AudioChannels { get; set; }

    [JsonPropertyName("LanguagePreference")]
    public int LanguagePreference { get; set; }

    [JsonPropertyName("DynamicRange")]
    public string DynamicRange { get; set; }

    [JsonPropertyName("Container")]
    public string Container { get; set; }

    [JsonPropertyName("DownloaderOptions")]
    public DownloaderOptionsDto DownloaderOptions { get; set; }
}