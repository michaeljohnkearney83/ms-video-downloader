

using MS.Vid_dldr.DownloadUpdater.Dtos;
using MS.Vid_dldr.DownloadUpdater.Dtos.Extract;
using MS.Vid_dldr.Manager.Protos.Common;

namespace MS.Vid_dldr.DownloaderUpdater.Mappers;

public static class ConfigureVdMapper
{
    public static ExtractInfo Map(ExtractInfoDto extractInfoDto)
    {
        var extractInfoProto = new ExtractInfo();

        if (extractInfoDto?.Formats == null) return extractInfoProto;

        foreach (var extractInfoDtoFormat in extractInfoDto.Formats)
        {
            var fragmentProtos = new List<Fragment>();
            if (extractInfoDtoFormat.Fragments != null)
            {
                foreach (var extractInfoDtoFormatFragment in extractInfoDtoFormat.Fragments)
                {
                    fragmentProtos.Add(new Fragment
                    {
                        Url = extractInfoDtoFormatFragment.Url,
                        Duration = !extractInfoDtoFormatFragment.Duration.HasValue ? 0.00 : extractInfoDtoFormatFragment.Duration.Value,
                    });
                }
            }

            var downloaderOptionProto = new DownloaderOption();
            if (extractInfoDtoFormat.DownloaderOptions != null)
            {
                downloaderOptionProto.HttpChunkSize = !extractInfoDtoFormat.DownloaderOptions.HttpChunkSize.HasValue ? 0 : extractInfoDtoFormat.DownloaderOptions.HttpChunkSize.Value;
            }

            var formatProto = new Format();
            formatProto.FormatId = string.IsNullOrWhiteSpace(extractInfoDtoFormat.FormatId) ? "" : extractInfoDtoFormat.FormatId;
            formatProto.FormatNote = string.IsNullOrWhiteSpace(extractInfoDtoFormat.FormatNote) ? "" : extractInfoDtoFormat.FormatNote;
            formatProto.Ext = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Ext) ? "" : extractInfoDtoFormat.Ext;
            formatProto.Protocol = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Protocol) ? "" : extractInfoDtoFormat.Protocol;
            formatProto.Acodec = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Acodec) ? "" : extractInfoDtoFormat.Acodec;
            formatProto.Vcodec = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Vcodec) ? "" : extractInfoDtoFormat.Vcodec;
            formatProto.Url = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Url) ? "" : extractInfoDtoFormat.Url;
            formatProto.Width = !extractInfoDtoFormat.Width.HasValue ? 0 : extractInfoDtoFormat.Width.Value;
            formatProto.Height = !extractInfoDtoFormat.Height.HasValue ? 0 : extractInfoDtoFormat.Height.Value;
            formatProto.Fps = !extractInfoDtoFormat.Fps.HasValue ? 0.00 : extractInfoDtoFormat.Fps.Value;
            formatProto.Rows = !extractInfoDtoFormat.Rows.HasValue ? 0 : extractInfoDtoFormat.Rows.Value;
            formatProto.Columns = !extractInfoDtoFormat.Columns.HasValue ? 0 : extractInfoDtoFormat.Columns.Value;
            formatProto.Fragments.Add(fragmentProtos);
            formatProto.AudioExt = string.IsNullOrWhiteSpace(extractInfoDtoFormat.AudioExt) ? "" : extractInfoDtoFormat.AudioExt;
            formatProto.VideoExt = string.IsNullOrWhiteSpace(extractInfoDtoFormat.VideoExt) ? "" : extractInfoDtoFormat.VideoExt;
            formatProto.Vbr = !extractInfoDtoFormat.Vbr.HasValue ? 0.00 : extractInfoDtoFormat.Vbr.Value;
            formatProto.Abr = !extractInfoDtoFormat.Abr.HasValue ? 0.00 : extractInfoDtoFormat.Abr.Value;
            formatProto.Tbr = !extractInfoDtoFormat.Tbr.HasValue ? 0.00 : extractInfoDtoFormat.Tbr.Value;
            formatProto.Resolution = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Resolution) ? "" : extractInfoDtoFormat.Resolution;
            formatProto.AspectRatio = !extractInfoDtoFormat.AspectRatio.HasValue ? 0.00 : extractInfoDtoFormat.AspectRatio.Value;
            formatProto.FilesizeApprox = !extractInfoDtoFormat.FilesizeApprox.HasValue ? 0 : extractInfoDtoFormat.FilesizeApprox.Value;
            //formatProto.HttpHeaders
            formatProto.FormatTitle = string.IsNullOrWhiteSpace(extractInfoDtoFormat.FormatTitle) ? "" : extractInfoDtoFormat.FormatTitle;
            //formatProto.FormatIndex
            formatProto.ManifestUrl = string.IsNullOrWhiteSpace(extractInfoDtoFormat.ManifestUrl) ? "" : extractInfoDtoFormat.ManifestUrl;
            formatProto.Language = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Language) ? "" : extractInfoDtoFormat.Language;
            //formatProto.Preference
            formatProto.Quality = !extractInfoDtoFormat.Quality.HasValue ? 0.00 : extractInfoDtoFormat.Quality.Value;
            formatProto.HasDrm = !extractInfoDtoFormat.HasDrm.HasValue ? 0 : Convert.ToInt32(extractInfoDtoFormat.HasDrm.Value);
            formatProto.SourcePreference = !extractInfoDtoFormat.SourcePreference.HasValue ? 0 : extractInfoDtoFormat.SourcePreference.Value;
            formatProto.Asr = !extractInfoDtoFormat.Asr.HasValue ? 0 : extractInfoDtoFormat.Asr.Value;
            formatProto.Filesize = !extractInfoDtoFormat.Filesize.HasValue ? 0 : extractInfoDtoFormat.Filesize.Value;
            formatProto.AudioChannels = !extractInfoDtoFormat.AudioChannels.HasValue ? 0 : extractInfoDtoFormat.AudioChannels.Value;
            formatProto.LanguagePreference = !extractInfoDtoFormat.LanguagePreference.HasValue ? 0 : extractInfoDtoFormat.LanguagePreference.Value;
            formatProto.DynamicRange = string.IsNullOrWhiteSpace(extractInfoDtoFormat.DynamicRange) ? "" : extractInfoDtoFormat.DynamicRange;
            formatProto.Container = string.IsNullOrWhiteSpace(extractInfoDtoFormat.Container) ? "" : extractInfoDtoFormat.Container;
            formatProto.DownloaderOptions = downloaderOptionProto;

            extractInfoProto.Formats.Add(formatProto);
        }

        return extractInfoProto;
    }
}