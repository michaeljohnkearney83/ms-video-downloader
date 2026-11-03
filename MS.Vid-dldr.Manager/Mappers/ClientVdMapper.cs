using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Dtos;
using dotNetConfigureProto = MS.Vid_dldr.Manager.Protos.FromConfigure;
using Google.Protobuf.WellKnownTypes;
using MS.Vid_dldr.Manager.Protos.Common;
using MS.Vid_dldr.Manager.Protos.FromConfigure;
using MS.Vid_dldr.Manager.Entities.Scripts;
using MS.Vid_dldr.Manager.Dtos.Extract;

namespace MS.Vid_dldr.Manager.Mappers;

public static class ClientVdMapper
{
    public static Protos.Common.ExtractInfo Map(ExtractInfoDto extractInfoDto)
    {
        var extractInfoProto = new Protos.Common.ExtractInfo();

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
                        Duration = extractInfoDtoFormatFragment.Duration,
                    });
                }
            }

            var downloaderOptionProto = new DownloaderOption();
            if (extractInfoDtoFormat.DownloaderOptions != null)
            {
                downloaderOptionProto.HttpChunkSize = extractInfoDtoFormat.DownloaderOptions.HttpChunkSize;
            }

            var formatProto = new Format();
            formatProto.FormatId = extractInfoDtoFormat.FormatId;
            formatProto.FormatNote = extractInfoDtoFormat.FormatNote;
            formatProto.Ext = extractInfoDtoFormat.Ext;
            formatProto.Protocol = extractInfoDtoFormat.Protocol;
            formatProto.Acodec = extractInfoDtoFormat.Acodec;
            formatProto.Vcodec = extractInfoDtoFormat.Vcodec;
            formatProto.Url = extractInfoDtoFormat.Url;
            formatProto.Width = extractInfoDtoFormat.Width;
            formatProto.Height = extractInfoDtoFormat.Height;
            formatProto.Fps = extractInfoDtoFormat.Fps;
            formatProto.Rows = extractInfoDtoFormat.Rows;
            formatProto.Columns = extractInfoDtoFormat.Columns;
            formatProto.Fragments.Add(fragmentProtos);
            formatProto.AudioExt = extractInfoDtoFormat.AudioExt;
            formatProto.VideoExt = extractInfoDtoFormat.VideoExt;
            formatProto.Vbr = extractInfoDtoFormat.Vbr;
            formatProto.Abr = extractInfoDtoFormat.Abr;
            formatProto.Tbr = extractInfoDtoFormat.Tbr;
            formatProto.Resolution = extractInfoDtoFormat.Resolution;
            formatProto.AspectRatio = extractInfoDtoFormat.AspectRatio;
            formatProto.FilesizeApprox = extractInfoDtoFormat.FilesizeApprox;
            //formatProto.HttpHeaders
            formatProto.FormatTitle = extractInfoDtoFormat.FormatTitle;
            //formatProto.FormatIndex
            formatProto.ManifestUrl = extractInfoDtoFormat.ManifestUrl;
            formatProto.Language = extractInfoDtoFormat.Language;
            //formatProto.Preference
            formatProto.Quality = extractInfoDtoFormat.Quality;
            formatProto.HasDrm = extractInfoDtoFormat.HasDrm;
            formatProto.SourcePreference = extractInfoDtoFormat.SourcePreference;
            formatProto.Asr = extractInfoDtoFormat.Asr;
            formatProto.Filesize = extractInfoDtoFormat.Filesize;
            formatProto.AudioChannels = extractInfoDtoFormat.AudioChannels;
            formatProto.LanguagePreference = extractInfoDtoFormat.LanguagePreference;
            formatProto.DynamicRange = extractInfoDtoFormat.DynamicRange;
            formatProto.Container = extractInfoDtoFormat.Container;
            formatProto.DownloaderOptions = downloaderOptionProto;

            extractInfoProto.Formats.Add(formatProto);
        }

        return extractInfoProto;
    }
}