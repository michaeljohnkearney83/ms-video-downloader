using Grpc.Core;
using AutoMapper;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf.Collections;
using Microsoft.AspNetCore.Mvc;
using MS.Vid_dldr.Manager.Protos.FromClient;
using MS.Vid_dldr.Manager.Data;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Messaging;
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Protos.Common;
using System.Text;
using MS.Vid_dldr.Manager.Dtos.Extract;
using MS.Vid_dldr.Manager.Mappers;
using System.Net.Http.Headers;
using System.Diagnostics;
using MS.Vid_dldr.Manager.Observability;

namespace MS.Vid_dldr.Manager.Services;

[ApiExplorerSettings(GroupName = "ClientService")]
public class VdApiCallsForClientService : VdapiCallsForClient.VdapiCallsForClientBase
{
    private readonly ILogger<VdApiCallsForClientService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;
    private readonly IDownloadRequestPublisher _publisher;

    public VdApiCallsForClientService(ILogger<VdApiCallsForClientService> logger, AppDbContext dbContext, IMapper mapper, IAppSettings appSettings, IServiceHelper serviceHelper,
        IDownloadRequestPublisher publisher)
    {
        _logger = logger;
        _dbContext = dbContext;
        _mapper = mapper;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
        _publisher = publisher;
    }

    public override async Task<GetSessionIdResponse> GetSessionId(GetSessionIdRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetSessionIdResponse>(request, "GetSessionId");

        try
        {
            var wasSuccessful = false;
            var downloadEnts = Enumerable.Empty<Download>();

            var dateTimeNowUtc = UtilityHelper.GetDateTimeNowUtc();
            var downloadNewEnt = new DownloadSession
            {
                Username = request.Username,
                Fid = request.Fid,
                ClientName = request.ClientName,
                DeploymentNo = request.DeploymentNo,
                RequestedOn = dateTimeNowUtc,
                LastActivity = dateTimeNowUtc
            };

            var downloadSessionExistingEnt = await _dbContext.DownloadSessions.FirstOrDefaultAsync(x => x.Username.ToLower() == request.Username.ToLower()
                && x.Fid.ToLower() == request.Fid.ToLower()
                && x.ClientName.ToLower() == request.ClientName.ToLower()
                && x.DeploymentNo.ToLower() == request.DeploymentNo.ToLower());

            if (downloadSessionExistingEnt == null)
            {
                await _dbContext.DownloadSessions.AddAsync(downloadNewEnt);
                wasSuccessful = await _serviceHelper.SaveAsync();
            }
            else
            {
                downloadEnts = _dbContext.Downloads.Where(x => x.DownloadSessionId == downloadSessionExistingEnt.Id
                                                                            && x.Status != "DISTRIBUTED")
                                                                            .OrderByDescending(x => x.RequestedOn);

                downloadSessionExistingEnt.LastActivity = dateTimeNowUtc;
                wasSuccessful = await _serviceHelper.SaveAsync();
            }


            response.DownloadSesssionId = (downloadSessionExistingEnt == null) ? downloadNewEnt.Id.ToString() : downloadSessionExistingEnt.Id.ToString();
            response.Feedback.ErrorMessage = (downloadSessionExistingEnt == null && !wasSuccessful) ? "Failed to get session id" : "";

            foreach (var downloadEnt in downloadEnts)
            {
                response.Downloads.Add(new DownloadProgressResponse
                {
                    ClientId = downloadEnt.ClientId.ToString(),
                    RequestedDownloadName = $"{downloadEnt.RequestedDownloadName}.{downloadEnt.RequestedDownloadExt}",
                    RequestedUrl = downloadEnt.RequestedUrl,
                    RequestedOn = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadEnt.RequestedOn),
                    Status = downloadEnt.Status,
                    ProgressPercentage = string.IsNullOrWhiteSpace(downloadEnt.ProgressPercent) ? "null" : downloadEnt.ProgressPercent,
                });
            }
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<GetDownloadsBySessionIdResponse> GetDownloadsBySessionId(GetDownloadsBySessionIdRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsBySessionIdResponse>(request, "GetDownloadsBySessionId");

        try
        {
            var downloadEnts = _dbContext.Downloads.Where(x => x.DownloadSessionId == Guid.Parse(request.DownloadSesssionId))
                                                   .OrderByDescending(x => x.RequestedOn);

            foreach (var downloadEnt in downloadEnts)
            {
                response.Downloads.Add(new DownloadProgressResponse
                {
                    ClientId = downloadEnt.ClientId.ToString(),
                    RequestedDownloadName = $"{downloadEnt.RequestedDownloadName}.{downloadEnt.RequestedDownloadExt}",
                    RequestedUrl = downloadEnt.RequestedUrl,
                    RequestedOn = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(downloadEnt.RequestedOn),
                    Status = downloadEnt.Status,
                    ProgressPercentage = string.IsNullOrWhiteSpace(downloadEnt.ProgressPercent) ? "null" : downloadEnt.ProgressPercent,
                    Feedback = new Feedback { ErrorMessage = !string.IsNullOrWhiteSpace(downloadEnt.ErrorMessage) ? downloadEnt.ErrorMessage : "" },
                });
            }

            response.Feedback.ErrorMessage = downloadEnts == null ? $"Could not find Downloads by Id: {request.DownloadSesssionId}" : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<RequestDownloadResponse> RequestDownload(RequestDownloadRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<RequestDownloadResponse>(request, "RequestDownload");

        try
        {
            if ($@"{request.RequestedDownloadFolder}\\{request.RequestedDownloadName}.{request.RequestedDownloadExt}".Length > _appSettings.MaxFilepathLength)
                throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, $"The filepath exceeds the max length {_appSettings.MaxFilepathLength}"));

            var clientId = Guid.NewGuid();
            Activity.Current?.SetTag(Telemetry.ClientIdTag, clientId.ToString());
            var activeDownloadExists = 0;

            if (request.Force == 0)
            {
                var downloadActive = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.RequestedUrl == request.RequestedUrl
                && x.DownloadSessionId == Guid.Parse(request.DownloadSessionId)
                && x.Status == Constants.DownloadStatusTypeRequested
                || x.Status == Constants.DownloadStatusTypeQueued
                || x.Status == Constants.DownloadStatusTypeAnalysis
                || x.Status == Constants.DownloadStatusTypeDownloading);

                if (downloadActive != null)
                {
                    clientId = downloadActive.ClientId;
                    activeDownloadExists = 1;
                }
            }

            if (activeDownloadExists == 0)
            {
                var downloadEnt = _mapper.Map<Download>(request);

                if (string.IsNullOrWhiteSpace(downloadEnt.RequestedDownloadFolder) || downloadEnt.RequestedDownloadFolder == "")
                {
                    var downloadSessionEnt = await _dbContext.DownloadSessions.FirstAsync(x => x.Id == downloadEnt.DownloadSessionId);

                    var deplNo = downloadSessionEnt.DeploymentNo;
                    downloadEnt.RequestedDownloadFolder = $"{_appSettings.HomeDrivePath}{deplNo}\\";
                }

                if (string.IsNullOrWhiteSpace(downloadEnt.RequestedDownloadExt) || downloadEnt.RequestedDownloadExt == "")
                {
                    var defaultDownloadExt = "mp4";

                    downloadEnt.RequestedDownloadExt = defaultDownloadExt;
                }

                downloadEnt.ClientId = clientId;
                downloadEnt.Status = Constants.DownloadStatusTypeRequested;
                downloadEnt.RequestedOn = downloadEnt.LastActivity = UtilityHelper.GetDateTimeNowUtc();
                downloadEnt.RetryOnErroredAttemptNo = 0;
                downloadEnt.RetryOnErroredMaxCount = _appSettings.RetryOnErroredMaxCount;
                await _dbContext.Downloads.AddAsync(downloadEnt);
                await _serviceHelper.SaveAsync();

                response.ClientId = clientId.ToString();

                var downloadDto = _mapper.Map<DownloadRequestDto>(downloadEnt);

                await _publisher.SendAsync(downloadDto);
                Activity.Current?.SetTag(Telemetry.DownloadIdTag, downloadDto.Id);
                Telemetry.DownloadsRequested.Add(1);
                _logger.LogInformation("Download {DownloadId} ({ClientId}) queued for {RequestedUrl}",
                    downloadDto.Id, clientId, downloadDto.RequestedUrl);

                downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.Id == downloadEnt.Id);
                if (downloadEnt != null)
                {
                    downloadEnt.Status = Constants.DownloadStatusTypeQueued;
                    downloadEnt.LastActivity = UtilityHelper.GetDateTimeNowUtc();
                    await _serviceHelper.SaveAsync();
                }
            }

            response.ActiveDownloadExists = activeDownloadExists;
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override Task<DownloadActionResponse> PauseDownload(DownloadActionRequest request, ServerCallContext context) =>
        ApplyActionAsync(DownloadActions.Pause, request, context);

    public override Task<DownloadActionResponse> ResumeDownload(DownloadActionRequest request, ServerCallContext context) =>
        ApplyActionAsync(DownloadActions.Resume, request, context);

    public override Task<DownloadActionResponse> CancelDownload(DownloadActionRequest request, ServerCallContext context) =>
        ApplyActionAsync(DownloadActions.Cancel, request, context);

    /// <summary>
    /// Pause / cancel: save PAUSED or CANCELLED first (so late progress from the Downloader is ignored), then publish
    /// the control message. Resume: send the download again with resume=true, then mark it QUEUED.
    /// </summary>
    private async Task<DownloadActionResponse> ApplyActionAsync(string action, DownloadActionRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<DownloadActionResponse>(request, $"{action} download");
        response.ClientId = request.ClientId;

        try
        {
            if (!Guid.TryParse(request.ClientId, out var clientId))
            {
                response.Feedback.ErrorMessage = $"Invalid download id: {request.ClientId}";
                return response;
            }
            Activity.Current?.SetTag(Telemetry.ClientIdTag, clientId.ToString());

            var downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.ClientId == clientId, context.CancellationToken);
            if (downloadEnt == null)
            {
                response.Feedback.ErrorMessage = $"Could not find download {clientId}";
                return response;
            }
            Activity.Current?.SetTag(Telemetry.DownloadIdTag, downloadEnt.Id);

            var notAllowed = DownloadActions.Validate(action, downloadEnt.Status);
            if (notAllowed != null)
            {
                response.Status = downloadEnt.Status;
                response.Feedback.ErrorMessage = notAllowed;
                return response;
            }

            var previousStatus = downloadEnt.Status;
            if (action == DownloadActions.Resume)
            {
                var downloadDto = _mapper.Map<DownloadRequestDto>(downloadEnt);
                downloadDto.Resume = true;
                await _publisher.SendAsync(downloadDto, context.CancellationToken);
                downloadEnt.Status = Constants.DownloadStatusTypeQueued;
            }
            else
            {
                downloadEnt.Status = action == DownloadActions.Pause
                    ? Constants.DownloadStatusTypePaused
                    : Constants.DownloadStatusTypeCancelled;
            }
            downloadEnt.LastActivity = UtilityHelper.GetDateTimeNowUtc();
            await _serviceHelper.SaveAsync();

            if (action != DownloadActions.Resume)
                await _publisher.SendControlAsync(new DownloadControlDto { Action = action, Id = downloadEnt.Id, ClientId = clientId },
                    context.CancellationToken);

            Telemetry.DownloadActions.Add(1, new KeyValuePair<string, object?>("vd.download.action", action));
            _logger.LogInformation("{Action} download {DownloadId} ({ClientId}): {PreviousStatus} -> {Status}",
                action, downloadEnt.Id, clientId, previousStatus, downloadEnt.Status);

            response.Status = downloadEnt.Status;
        }
        catch (Exception ex)
        {
            response.Feedback.ErrorMessage = $"{action} failed: {ex.Message}";
            response.Feedback.Exception = ex.ToString();
            _logger.LogError(ex, "{Action} failed for download {ClientId}", action, request.ClientId);
        }

        return response;
    }

    public override async Task<RequestExtractInfoFormatsResponse> RequestExtractInfoFormats(RequestExtractInfoFormatsRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<RequestExtractInfoFormatsResponse>(request, "RequestExtractInfoFormats");

        try
        {
            var activeExtractInfosExists = 0;
            JsonDocument formats = null;
            string errorMessage = null;
            int timeoutMs = (request.TimeoutMs == -1 || request.TimeoutMs == 0)
                ? _appSettings.ExtractInfoFormatTimeoutMs : request.TimeoutMs;

            var extractInfosActive = await _dbContext.ExtractInfos
                .OrderByDescending(x => x.RequestedOn)
                .FirstOrDefaultAsync(x => x.RequestedUrl == request.RequestedUrl
                    && x.DownloadSessionId == Guid.Parse(request.DownloadSessionId)
                    && x.InfoType == Constants.ExtractInfoTypeFormats
                    && x.RequestedUrl.ToLower() == request.RequestedUrl.ToLower()
                    && x.Logged.HasValue
                    && !x.Response.HasValue);

            if (extractInfosActive != null)
            {
                activeExtractInfosExists = 1;
                formats = extractInfosActive.Info;
                errorMessage = !string.IsNullOrWhiteSpace(extractInfosActive.ErrorMessage) ? extractInfosActive.ErrorMessage : "";

                extractInfosActive.Info = null;
                extractInfosActive.Response = UtilityHelper.GetDateTimeNowUtc();

                _dbContext.ExtractInfos.Update(extractInfosActive);
                await _serviceHelper.SaveAsync();
            }

            if (activeExtractInfosExists == 0)
            {
                var newExtractInfoId = -1;
                var extractInfoEnt = _mapper.Map<Entities.ExtractInfo>(request);
                extractInfoEnt.RequestedOn = UtilityHelper.GetDateTimeNowUtc();
                extractInfoEnt.InfoType = Constants.ExtractInfoTypeFormats;

                await _dbContext.ExtractInfos.AddAsync(extractInfoEnt);
                await _dbContext.SaveChangesAsync();

                newExtractInfoId = extractInfoEnt.Id;
                _dbContext.ExtractInfos.Update(extractInfoEnt).State = EntityState.Detached;

                var requestExtractInfoDto = new ExtractInfoRequestDto
                {
                    Id = newExtractInfoId,
                    RequestedUrl = request.RequestedUrl
                };
                await _publisher.SendAsync(requestExtractInfoDto);

                var startDate = DateTime.Now;
                var msProgress = timeoutMs;
                Entities.ExtractInfo? loggedExtractInfoEnt = null;

                while (true)
                {
                    // TotalMilliseconds: .Milliseconds is only the 0-999 part, so the timeout never fired and a
                    // failed extraction kept this request polling Postgres in a tight loop.
                    msProgress = (int)DateTime.Now.Subtract(startDate).TotalMilliseconds;

                    if (loggedExtractInfoEnt != null
                        || msProgress > timeoutMs)
                        break;

                    await Task.Delay(100, context.CancellationToken);

                    loggedExtractInfoEnt = await _dbContext.ExtractInfos
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == newExtractInfoId
                            && x.Logged.HasValue);
                }

                if (loggedExtractInfoEnt != null)
                {
                    response.Feedback.InformationMessage = $"Formats aquired in {TimeSpan.FromMilliseconds(msProgress).TotalSeconds} seconds";

                    formats = loggedExtractInfoEnt.Info;

                    loggedExtractInfoEnt.Info = null;
                    loggedExtractInfoEnt.Response = UtilityHelper.GetDateTimeNowUtc();

                    _dbContext.ExtractInfos.Update(loggedExtractInfoEnt).State = EntityState.Modified;
                    await _serviceHelper.SaveAsync();
                }
                else
                    response.Feedback.WarningMessage = "Timed out without getting Formats";
            }

            response.ActiveExtractInfoExisted = activeExtractInfosExists;
            var extractInfoProto = new Protos.Common.ExtractInfo();
            if (formats != null)
            {
                var extractInfoDto = formats.RootElement.Deserialize<ExtractInfoDto>();
                extractInfoProto = ClientVdMapper.Map(extractInfoDto);
            }
            response.ExtractInfo = extractInfoProto;
            response.Feedback.ErrorMessage = !string.IsNullOrWhiteSpace(errorMessage) ? errorMessage : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
}