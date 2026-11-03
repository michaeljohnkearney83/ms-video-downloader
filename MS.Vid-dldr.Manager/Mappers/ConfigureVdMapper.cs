using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Dtos;
using dotNetConfigureProto = MS.Vid_dldr.Manager.Protos.FromConfigure;
using Google.Protobuf.WellKnownTypes;
using MS.Vid_dldr.Manager.Protos.Common;
using MS.Vid_dldr.Manager.Protos.FromConfigure;
using MS.Vid_dldr.Manager.Entities.Scripts;
using MS.Vid_dldr.Manager.Helpers;

namespace MS.Vid_dldr.Manager.Mappers;

public static class ConfigureVdMapper
{
    public static List<dotNetConfigureProto.ConfigureDownload> Map(List<Download> downloadEnts)
    {
        var downloadsProto = new List<dotNetConfigureProto.ConfigureDownload>();

        if (downloadEnts == null) return downloadsProto;

        foreach (var downloadEnt in downloadEnts)
        {
            downloadsProto.Add(new ConfigureDownload
            {
                Id = downloadEnt.Id,
                SessionId = downloadEnt.DownloadSession.Id.ToString(),
                ForceId = downloadEnt.DownloadSession.Fid,
                Username = downloadEnt.DownloadSession.Username,
                Client = downloadEnt.DownloadSession.ClientName,
                DeploymentNo = downloadEnt.DownloadSession.DeploymentNo,
                Requested = new DownloadRequested
                {
                    ClientId = downloadEnt.ClientId.ToString(),
                    On = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadEnt.RequestedOn),
                    Url = downloadEnt.RequestedUrl,
                    Folder = downloadEnt.RequestedDownloadFolder,
                    Name = downloadEnt.RequestedDownloadName,
                    Ext = downloadEnt.RequestedDownloadExt,
                    RetryCount = downloadEnt.RequestedRetryCount,
                    TimeoutSeconds = downloadEnt.RequestedTimeoutSeconds,
                    StalledTimeoutSeconds = downloadEnt.RequestedDownloadStalledTimeoutSeconds,
                },
                VideoAnalysis = new VideoAnalysis
                {
                    Id = !string.IsNullOrWhiteSpace(downloadEnt.VideoId) ? downloadEnt.VideoId : "",
                    Title = !string.IsNullOrWhiteSpace(downloadEnt.VideoTitle) ? downloadEnt.VideoTitle : "",
                    Description = !string.IsNullOrWhiteSpace(downloadEnt.VideoDescription) ? downloadEnt.VideoDescription : "",
                    Duration = !string.IsNullOrWhiteSpace(downloadEnt.VideoDuration) ? downloadEnt.VideoDuration : "",
                    Thumbnail = !string.IsNullOrWhiteSpace(downloadEnt.VideoThumbnail) ? downloadEnt.VideoThumbnail : "",
                    Url = !string.IsNullOrWhiteSpace(downloadEnt.VideoId) ? downloadEnt.VideoId : "",
                    Resolution = !string.IsNullOrWhiteSpace(downloadEnt.VideoResolution) ? downloadEnt.VideoResolution : "",
                    AspectRatio = !string.IsNullOrWhiteSpace(downloadEnt.VideoAspecRatio) ? downloadEnt.VideoAspecRatio : "",
                    FileSizeAprox = !string.IsNullOrWhiteSpace(downloadEnt.VideoFileSizeApprox) ? downloadEnt.VideoFileSizeApprox : "",
                },
                LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadEnt.LastActivity),
                Status = !string.IsNullOrWhiteSpace(downloadEnt.Status) ? downloadEnt.Status : "UNKNOWN",
                Filename = !string.IsNullOrWhiteSpace(downloadEnt.Filename) ? downloadEnt.Filename : "",
                Progress = new DownloadProgress
                {
                    Eta = !string.IsNullOrWhiteSpace(downloadEnt.ProgressEta) ? downloadEnt.ProgressEta : "",
                    Speed = !string.IsNullOrWhiteSpace(downloadEnt.ProgressSpeed) ? downloadEnt.ProgressSpeed : "",
                    Percent = !string.IsNullOrWhiteSpace(downloadEnt.ProgressPercent) ? downloadEnt.ProgressPercent : "",
                    Elapsed = !string.IsNullOrWhiteSpace(downloadEnt.ProgressElapsed) ? downloadEnt.ProgressElapsed : "",
                    DefaultTemplate = !string.IsNullOrWhiteSpace(downloadEnt.ProgressDefaultTemplate) ? downloadEnt.ProgressDefaultTemplate : "",
                },
                ErrorMessage = !string.IsNullOrWhiteSpace(downloadEnt.ErrorMessage) ? downloadEnt.ErrorMessage : "",
            });
        }

        return downloadsProto;
    }

    public static dotNetConfigureProto.ConfigureTableDownloadSession MapTable(DownloadSession downloadSessionEnt)
    {
        var downloadSessionProto = new dotNetConfigureProto.ConfigureTableDownloadSession();

        if (downloadSessionEnt == null) return downloadSessionProto;

        downloadSessionProto.Id = downloadSessionEnt.Id.ToString();
        downloadSessionProto.Username = downloadSessionEnt.Username;
        downloadSessionProto.ForceId = downloadSessionEnt.Fid;
        downloadSessionProto.Client = downloadSessionEnt.ClientName;
        downloadSessionProto.DeploymentNo = downloadSessionEnt.DeploymentNo;
        downloadSessionProto.RequestedOn = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadSessionEnt.RequestedOn);
        downloadSessionProto.LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadSessionEnt.LastActivity);
        downloadSessionProto.Disabled = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadSessionEnt.Disabled != null ? downloadSessionEnt.Disabled.Value : UtilityHelper.GetMinWithKind(DateTimeKind.Utc));

        return downloadSessionProto;
    }

    public static List<dotNetConfigureProto.ConfigureTableDownloadSession> MapTable(List<DownloadSession> downloadSessionEnts)
    {
        var downloadSessionsProto = new List<dotNetConfigureProto.ConfigureTableDownloadSession>();

        if (downloadSessionEnts == null) return downloadSessionsProto;

        foreach (var downloadSessionEnt in downloadSessionEnts)
        {
            downloadSessionsProto.Add(new ConfigureTableDownloadSession
            {
                Id = downloadSessionEnt.Id.ToString(),
                Username = downloadSessionEnt.Username,
                ForceId = downloadSessionEnt.Fid,
                Client = downloadSessionEnt.ClientName,
                DeploymentNo = downloadSessionEnt.DeploymentNo,
                RequestedOn = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadSessionEnt.RequestedOn),
                LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadSessionEnt.LastActivity),
                Disabled = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadSessionEnt.Disabled != null ? downloadSessionEnt.Disabled.Value : UtilityHelper.GetMinWithKind(DateTimeKind.Utc)),
            });
        }

        return downloadSessionsProto;
    }

    public static List<dotNetConfigureProto.ConfigureTableDownload> MapTable(List<Download> downloadEnts)
    {
        var downloadsProto = new List<dotNetConfigureProto.ConfigureTableDownload>();

        if (downloadEnts == null) return downloadsProto;

        foreach (var downloadEnt in downloadEnts)
        {
            downloadsProto.Add(new ConfigureTableDownload
            {
                Id = downloadEnt.Id,
                SessionId = downloadEnt.DownloadSessionId.ToString(),
                Requested = new DownloadRequested
                {
                    ClientId = downloadEnt.ClientId.ToString(),
                    On = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadEnt.RequestedOn),
                    Url = downloadEnt.RequestedUrl,
                    Folder = downloadEnt.RequestedDownloadFolder,
                    Name = downloadEnt.RequestedDownloadName,
                    Ext = downloadEnt.RequestedDownloadExt,
                    RetryCount = downloadEnt.RequestedRetryCount,
                    TimeoutSeconds = downloadEnt.RequestedTimeoutSeconds,
                    StalledTimeoutSeconds = downloadEnt.RequestedDownloadStalledTimeoutSeconds,
                },
                VideoAnalysis = new VideoAnalysis
                {
                    Id = !string.IsNullOrWhiteSpace(downloadEnt.VideoId) ? downloadEnt.VideoId : "",
                    Title = !string.IsNullOrWhiteSpace(downloadEnt.VideoTitle) ? downloadEnt.VideoTitle : "",
                    Description = !string.IsNullOrWhiteSpace(downloadEnt.VideoDescription) ? downloadEnt.VideoDescription : "",
                    Duration = !string.IsNullOrWhiteSpace(downloadEnt.VideoDuration) ? downloadEnt.VideoDuration : "",
                    Thumbnail = !string.IsNullOrWhiteSpace(downloadEnt.VideoThumbnail) ? downloadEnt.VideoThumbnail : "",
                    Url = !string.IsNullOrWhiteSpace(downloadEnt.VideoId) ? downloadEnt.VideoId : "",
                    Resolution = !string.IsNullOrWhiteSpace(downloadEnt.VideoResolution) ? downloadEnt.VideoResolution : "",
                    AspectRatio = !string.IsNullOrWhiteSpace(downloadEnt.VideoAspecRatio) ? downloadEnt.VideoAspecRatio : "",
                    FileSizeAprox = !string.IsNullOrWhiteSpace(downloadEnt.VideoFileSizeApprox) ? downloadEnt.VideoFileSizeApprox : "",
                },
                LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadEnt.LastActivity),
                Status = !string.IsNullOrWhiteSpace(downloadEnt.Status) ? downloadEnt.Status : "UNKNOWN",
                Filename = !string.IsNullOrWhiteSpace(downloadEnt.Filename) ? downloadEnt.Filename : "",
                Progress = new DownloadProgress
                {
                    Eta = !string.IsNullOrWhiteSpace(downloadEnt.ProgressEta) ? downloadEnt.ProgressEta : "",
                    Speed = !string.IsNullOrWhiteSpace(downloadEnt.ProgressSpeed) ? downloadEnt.ProgressSpeed : "",
                    Percent = !string.IsNullOrWhiteSpace(downloadEnt.ProgressPercent) ? downloadEnt.ProgressPercent : "",
                    Elapsed = !string.IsNullOrWhiteSpace(downloadEnt.ProgressElapsed) ? downloadEnt.ProgressElapsed : "",
                    DefaultTemplate = !string.IsNullOrWhiteSpace(downloadEnt.ProgressDefaultTemplate) ? downloadEnt.ProgressDefaultTemplate : "",
                },
                ErrorMessage = !string.IsNullOrWhiteSpace(downloadEnt.ErrorMessage) ? downloadEnt.ErrorMessage : "",
                RetryOnErroredAttemptNo = downloadEnt.RetryOnErroredAttemptNo,
                RetryOnErroredMaxCount = downloadEnt.RetryOnErroredMaxCount,
                Analysesd = Convert.ToInt32(downloadEnt.Analysed.HasValue),
            });
        }

        return downloadsProto;
    }

    public static List<dotNetConfigureProto.DownloadStatusCounts> Map(List<DownloadStatusDuringPeriod> downloadStatusDuringPeriodEnts)
    {
        var downloadStatusCountsProtos = new List<dotNetConfigureProto.DownloadStatusCounts>();

        if (downloadStatusDuringPeriodEnts == null) return downloadStatusCountsProtos;

        foreach (var downloadStatusDuringPeriodEnt in downloadStatusDuringPeriodEnts)
        {
            downloadStatusCountsProtos.Add(new DownloadStatusCounts
            {
                RequestedOn = downloadStatusDuringPeriodEnt.requestedon.ToString("yyy-MM-dd"),
                None = downloadStatusDuringPeriodEnt.NONE,
                Requested = downloadStatusDuringPeriodEnt.REQUESTED,
                Queued = downloadStatusDuringPeriodEnt.QUEUED,
                Analysis = downloadStatusDuringPeriodEnt.ANALYSIS,
                Downloading = downloadStatusDuringPeriodEnt.DOWNLOADING,
                Finished = downloadStatusDuringPeriodEnt.FINISHED,
                DistributedLookup = downloadStatusDuringPeriodEnt.DISTRIBUTED_LOOKUP,
                Distributed = downloadStatusDuringPeriodEnt.DISTRIBUTED,
                Deleted = downloadStatusDuringPeriodEnt.DELETED,
                Errored = downloadStatusDuringPeriodEnt.ERRORED,
                Retry = downloadStatusDuringPeriodEnt.RETRY
            });
        }

        return downloadStatusCountsProtos;
    }

    public static List<dotNetConfigureProto.ConfigureError> Map_ToError(List<Download> downloadEnts)
    {
        var configureErrorProtos = new List<dotNetConfigureProto.ConfigureError>();

        if (downloadEnts == null) return configureErrorProtos;

        foreach (var downloadEnt in downloadEnts)
        {
            configureErrorProtos.Add(new dotNetConfigureProto.ConfigureError
            {
                Id = downloadEnt.Id,
                LastActivity = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadEnt.LastActivity),
                ErrorMessage = !string.IsNullOrWhiteSpace(downloadEnt.ErrorMessage) ? downloadEnt.ErrorMessage : "",
            });
        }

        return configureErrorProtos;
    }
}