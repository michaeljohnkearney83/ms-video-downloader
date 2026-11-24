using Grpc.Core;
using AutoMapper;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf.Collections;
using Microsoft.AspNetCore.Mvc;
using MS.Vid_dldr.Manager.Protos;
using MS.Vid_dldr.Manager.Data;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Messaging;
using MS.Vid_dldr.Manager.Helpers;
using Google.Protobuf;
using MS.Vid_dldr.Manager.Observability;

namespace MS.Vid_dldr.Manager.Services.Internal;

[ApiExplorerSettings(GroupName = "UpdaterService")]
public class VdApiCallsForUpdaterService : VdApiCallsForUpdater.VdApiCallsForUpdaterBase
{
    private readonly ILogger<VdApiCallsForUpdaterService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;
    private readonly IDownloadRequestPublisher _publisher;

    public VdApiCallsForUpdaterService(ILogger<VdApiCallsForUpdaterService> logger, AppDbContext dbContext, IMapper mapper, IAppSettings appSettings, IServiceHelper serviceHelper,
        IDownloadRequestPublisher publisher)
    {
        _logger = logger;
        _dbContext = dbContext;
        _mapper = mapper;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
        _publisher = publisher;
    }

    public override async Task<UpdateDownloadProgressAnalysisResponse> UpdateDownloadProgressAnalysis(UpdateDownloadProgressAnalysisRequest request, ServerCallContext context)
    {
        Telemetry.RecordStatusUpdate(request.Id, "ANALYSIS");
        var response = _serviceHelper.SetupMethod<UpdateDownloadProgressAnalysisResponse>(request, "UpdateDownloadProgressAnalysis");

        try
        {
            if (request.Status != Constants.DownloadStatusTypeAnalysis)
                throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, $"Wrong progress status {request.Status} for Analysis method"));

            var downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.Id == request.Id);
            if (downloadEnt != null)
            {
                downloadEnt.Analysed = request.LastActivity.ToDateTime();
                downloadEnt.VideoId = request.VideoId;
                downloadEnt.VideoTitle = request.VideoTitle;
                downloadEnt.VideoDescription = request.VideoDescription;
                downloadEnt.VideoDuration = request.VideoDuration;
                downloadEnt.VideoThumbnail = request.VideoThumbnail;
                downloadEnt.VideoUrl = request.VideoUrl;
                downloadEnt.VideoResolution = request.VideoResolution;
                downloadEnt.VideoAspecRatio = request.VideoAspecRatio;
                downloadEnt.VideoFileSizeApprox = request.VideoFileSizeApprox;
                downloadEnt.LastActivity = request.LastActivity.ToDateTime();

                //1 in 20 Analysis fails (added time.sleep() in Downloader), retry on Finished if Analysed date is null
                if (downloadEnt.Status != Constants.DownloadStatusTypeFinished
                && downloadEnt.Status != Constants.DownloadStatusTypeDistributedLookup
                && downloadEnt.Status != Constants.DownloadStatusTypeDistributed)
                    downloadEnt.Status = _serviceHelper.TranslateStatus(request.Status);

                await _serviceHelper.SaveAsync();
            }

            response.Feedback.ErrorMessage = downloadEnt == null ? $"Could not find Download by Id: {request.Id}" : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<UpdateDownloadProgressDownloadingResponse> UpdateDownloadProgressDownloading(UpdateDownloadProgressDownloadingRequest request, ServerCallContext context)
    {
        Telemetry.RecordStatusUpdate(request.Id, "DOWNLOADING");
        var response = _serviceHelper.SetupMethod<UpdateDownloadProgressDownloadingResponse>(request, "UpdateDownloadProgressDownloading");

        try
        {
            if (request.Status != Constants.DownloadStatusTypeDownloading)
                throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, $"Wrong progress status {request.Status} for Downloading method"));

            var downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.Id == request.Id);
            if (downloadEnt != null)
            {
                downloadEnt.LastActivity = request.LastActivity.ToDateTime();
                downloadEnt.Status = _serviceHelper.TranslateStatus(request.Status);
                downloadEnt.ProgressEta = request.ProgressEta;
                downloadEnt.ProgressSpeed = request.ProgressSpeed;
                downloadEnt.ProgressPercent = request.ProgressPercent;
                downloadEnt.ProgressElapsed = request.ProgressElapsed;
                downloadEnt.ProgressDefaultTemplate = request.ProgressDefaultTemplate;
                downloadEnt.Filename = request.Filename;

                await _serviceHelper.SaveAsync();
            }
            response.Feedback.ErrorMessage = downloadEnt == null ? $"Could not find Download by Id: {request.Id}" : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<UpdateDownloadProgressFinishedResponse> UpdateDownloadProgressFinished(UpdateDownloadProgressFinishedRequest request, ServerCallContext context)
    {
        Telemetry.RecordStatusUpdate(request.Id, "FINISHED");
        var response = _serviceHelper.SetupMethod<UpdateDownloadProgressFinishedResponse>(request, "UpdateDownloadFinished");

        try
        {
            if (request.Status != Constants.DownloadStatusTypeFinished)
                throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, $"Wrong progress status {request.Status} for Finished method"));

            var downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.Id == request.Id);
            if (downloadEnt != null)
            {
                if (downloadEnt.Status != Constants.DownloadStatusTypeFinished
                && downloadEnt.Status != Constants.DownloadStatusTypeDistributedLookup
                && downloadEnt.Status != Constants.DownloadStatusTypeDistributed)
                {
                    downloadEnt.LastActivity = request.LastActivity.ToDateTime();
                    downloadEnt.Status = request.Status;
                    downloadEnt.ProgressEta = null;
                    downloadEnt.ProgressSpeed = null;
                    downloadEnt.ProgressPercent = null;
                    downloadEnt.ProgressElapsed = null;
                    downloadEnt.ProgressDefaultTemplate = null;
                    downloadEnt.ErrorMessage = null;

                    await _serviceHelper.SaveAsync();

                    if (downloadEnt.Analysed == null)
                    {
                        if (!await _serviceHelper.RetryAnalysisDownloadAsync(downloadEnt))
                            _logger.LogError($"UpdateDownloadProgressFinished(UpdateDownloadProgressFinishedRequest: {JsonSerializer.Serialize(request)}: Failed to retry analysis");
                    }
                }
            }
            response.Feedback.ErrorMessage = downloadEnt == null ? $"Could not find Download by Id: {request.Id}" : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<UpdateDownloadProgressErroredResponse> UpdateDownloadProgressErrored(UpdateDownloadProgressErroredRequest request, ServerCallContext context)
    {
        Telemetry.RecordStatusUpdate(request.Id, "ERRORED");
        var response = _serviceHelper.SetupMethod<UpdateDownloadProgressErroredResponse>(request, "UpdateDownloadProgressErrored");

        try
        {
            if (request.Status != Constants.DownloadStatusTypeErrored)
                throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, $"Wrong progress status {request.Status} for Errored method"));

            var downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.Id == request.Id);
            if (downloadEnt != null)
            {
                var doRetryOnErrored = downloadEnt.RetryOnErroredAttemptNo < downloadEnt.RetryOnErroredMaxCount;

                if (downloadEnt.Status != Constants.DownloadStatusTypeErrored
                    && downloadEnt.Status != Constants.DownloadStatusTypeDistributedLookup
                    && downloadEnt.Status != Constants.DownloadStatusTypeDistributed)
                {
                    downloadEnt.LastActivity = request.LastActivity.ToDateTime();
                    downloadEnt.Status = !doRetryOnErrored ? request.Status : Constants.DownloadStatusTypeRetry;
                    downloadEnt.ProgressEta = null;
                    downloadEnt.ProgressSpeed = null;
                    downloadEnt.ProgressPercent = null;
                    downloadEnt.ProgressElapsed = null;
                    downloadEnt.ProgressDefaultTemplate = null;
                    downloadEnt.ErrorMessage = string.IsNullOrWhiteSpace(request.ErrorMessage) ? null : request.ErrorMessage;
                    downloadEnt.RetryOnErroredAttemptNo = !doRetryOnErrored ? downloadEnt.RetryOnErroredAttemptNo : downloadEnt.RetryOnErroredAttemptNo + 1;
                    await _serviceHelper.SaveAsync();

                    if (doRetryOnErrored)
                    {
                        if (!await RetryRequestDownloadAsync(downloadEnt.Id, downloadEnt.RetryOnErroredAttemptNo))
                            _logger.LogError($"UpdateDownloadProgressErrored(UpdateDownloadProgressErroredRequest: {JsonSerializer.Serialize(request)}: Failed to retry download request");
                    }
                }
            }
            response.Feedback.ErrorMessage = downloadEnt == null ? $"Could not find Download by Id: {request.Id}" : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<SetVersionsResponse> SetVersions(SetVersionsRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<SetVersionsResponse>(request, "SetVersionsRequest");

        try
        {
            var newSoftwareVersionEnt = new SoftwareVersion
            {
                ManagerApp = request.ManagerApp,
                ManagerFramework = request.ManagerFramework,
                ManagerDb = request.ManagerDb,
                ManagerDbLastMigration = request.ManagerDbLastMigration,
                DownloaderApp = request.DownloaderApp,
                DownloaderFramework = request.DownloaderFramework,
                DownloaderYtdlp = request.DownloaderYtdlp,
                UpdaterApp = request.UpdaterApp,
                UpdaterFramework = request.UpdaterFramework,
                Requested = DateTime.SpecifyKind(DateTime.Parse(request.Requested), DateTimeKind.Utc),
                YtdlpGit = request.DownloaderYtdlp,
            };

            var existingSoftwareVersion = await _dbContext.SoftwareVersions.FirstOrDefaultAsync();

            if (existingSoftwareVersion != null)
            {
                existingSoftwareVersion.ManagerApp = newSoftwareVersionEnt.ManagerApp;
                existingSoftwareVersion.ManagerFramework = newSoftwareVersionEnt.ManagerFramework;
                existingSoftwareVersion.ManagerDb = newSoftwareVersionEnt.ManagerDb;
                existingSoftwareVersion.ManagerDbLastMigration = newSoftwareVersionEnt.ManagerDbLastMigration;
                existingSoftwareVersion.DownloaderApp = newSoftwareVersionEnt.DownloaderApp;
                existingSoftwareVersion.DownloaderFramework = newSoftwareVersionEnt.DownloaderFramework;
                existingSoftwareVersion.DownloaderYtdlp = newSoftwareVersionEnt.DownloaderYtdlp;
                existingSoftwareVersion.UpdaterApp = newSoftwareVersionEnt.UpdaterApp;
                existingSoftwareVersion.UpdaterFramework = newSoftwareVersionEnt.UpdaterFramework;
                existingSoftwareVersion.Requested = newSoftwareVersionEnt.Requested;
            }
            else
                _dbContext.SoftwareVersions.Add(newSoftwareVersionEnt);

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"{Constants.MsNoticeLogPrefix}{ex.ToString()}");
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<UpdateRequestExtractInfoFormatsResponse> UpdateRequestExtractInfoFormats(UpdateRequestExtractInfoFormatsRequest request, ServerCallContext context)
    {
        Telemetry.RecordStatusUpdate(request.Id, "EXTRACT_INFO");
        var response = _serviceHelper.SetupMethod<UpdateRequestExtractInfoFormatsResponse>(request, "UpdateRequestExtractInfoFormats");
        try
        {
            var extractInfoEnt = await _dbContext.ExtractInfos.FirstOrDefaultAsync(x => x.Id == request.Id
                && x.Info == null
                && !x.Response.HasValue
                && !x.Logged.HasValue);

            if (extractInfoEnt != null)
            {
                extractInfoEnt.Logged = UtilityHelper.GetDateTimeNowUtc();
                extractInfoEnt.InfoType = Constants.ExtractInfoTypeFormats;
                extractInfoEnt.Info = JsonSerializer.SerializeToDocument(request.ExtractInfo);

                await _serviceHelper.SaveAsync();
            }
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    private async Task<bool> RetryRequestDownloadAsync(int downloadId, int attemptNo)
    {
        _logger.LogInformation($"RetryRequestDownloadAsync(int downloadId: {downloadId}, int attemptNo: {attemptNo}");

        bool result = false;

        try
        {
            var downloadErrored = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.Id == downloadId
            && x.Status == Constants.DownloadStatusTypeRetry);

            if (downloadErrored != null)
            {
                //Queued it
                var downloadDto = _mapper.Map<DownloadRequestDto>(downloadErrored);

                await _publisher.SendAsync(downloadDto);

                downloadErrored.RetryOnErroredAttemptNo = attemptNo;
                downloadErrored.Status = Constants.DownloadStatusTypeQueued;
                downloadErrored.LastActivity = UtilityHelper.GetDateTimeNowUtc();
                await _serviceHelper.SaveAsync();
            }
            result = true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"{Constants.MsNoticeLogPrefix}{ex.ToString()}");
        }

        return result;
    }
}