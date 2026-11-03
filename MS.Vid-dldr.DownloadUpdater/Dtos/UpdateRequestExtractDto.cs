using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

namespace MS.Vid_dldr.DownloadUpdater.Dtos;

public class UpdateRequestExtractDto
{
    [JsonPropertyName("managerId")]
    public int Id { get; set; }
    [JsonPropertyName("extractInfo")]
    public ExtractInfoDto ExtractInfo { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}