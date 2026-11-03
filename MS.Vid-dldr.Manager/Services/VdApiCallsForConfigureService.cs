using Grpc.Core;
using AutoMapper;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf.Collections;
using Microsoft.AspNetCore.Mvc;
using MS.Vid_dldr.Manager.Protos.FromConfigure;
using MS.Vid_dldr.Manager.Data;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.RabbitMq;
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Protos.Common;
using MS.Vid_dldr.Manager.Mappers;

namespace MS.Vid_dldr.Manager.Services;

[ApiExplorerSettings(GroupName = "ConfigureService")]
public class VdApiCallsForConfigureService : VdApiCallsForConfigure.VdApiCallsForConfigureBase
{
    private readonly ILogger<VdApiCallsForConfigureService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;
    private readonly IVersionService _versionService;

    public VdApiCallsForConfigureService(ILogger<VdApiCallsForConfigureService> logger, AppDbContext dbContext, IMapper mapper, IAppSettings appSettings, IServiceHelper serviceHelper, IVersionService versionService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _mapper = mapper;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
        _versionService = versionService;
    }

    public override async Task<GetCollectiveVersionsResponse> GetCollectiveVersions(GetCollectiveVersionsRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetCollectiveVersionsResponse>(request, "GetCollectiveVersions");

        try
        {
            int timeoutMs = (request.TimeoutMs == -1 || request.TimeoutMs == 0)
                ? _appSettings.ExtractInfoFormatTimeoutMs : request.TimeoutMs;
            var startDate = DateTime.Now;
            var msProgress = timeoutMs;
            Entities.SoftwareVersion? softwareVersionEnt = null;
            var requestedDate = UtilityHelper.GetDateTimeNowUtc();
            var newSoftwareVersions = new SoftwareVersions();

            _versionService.InitiateCollectionOfVersions(requestedDate);

            while (true)
            {
                msProgress = DateTime.Now.Subtract(startDate).Milliseconds;

                if (softwareVersionEnt != null
                    || msProgress > timeoutMs)
                    break;

                softwareVersionEnt = await _dbContext.SoftwareVersions
                    .OrderByDescending(x => x.Requested == requestedDate)
                    .FirstOrDefaultAsync();
            }

            if (softwareVersionEnt != null)
            {
                response.Feedback.InformationMessage = $"Versions aquired in {TimeSpan.FromMilliseconds(msProgress).TotalSeconds} seconds";

                newSoftwareVersions.ManagerApp = softwareVersionEnt.ManagerApp;
                newSoftwareVersions.ManagerFramework = softwareVersionEnt.ManagerFramework;
                newSoftwareVersions.ManagerDb = softwareVersionEnt.ManagerDb;
                newSoftwareVersions.ManagerDbLastMigration = softwareVersionEnt.ManagerDbLastMigration;
                newSoftwareVersions.DownloaderApp = softwareVersionEnt.DownloaderApp;
                newSoftwareVersions.DownloaderFramework = softwareVersionEnt.DownloaderFramework;
                newSoftwareVersions.DownloaderYtdlp = softwareVersionEnt.DownloaderYtdlp;
                newSoftwareVersions.UpdaterApp = softwareVersionEnt.UpdaterApp;
                newSoftwareVersions.UpdaterFramework = softwareVersionEnt.UpdaterFramework;
                newSoftwareVersions.Requested = softwareVersionEnt.Requested.ToString();
                newSoftwareVersions.YtdlpGit = softwareVersionEnt.YtdlpGit;
            }
            else
                response.Feedback.WarningMessage = "Timed out without getting Versions";

            response.SoftwareVersions = newSoftwareVersions;
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDistributorUpResponse> GetDistributorUp(GetDistributorUpRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDistributorUpResponse>(request, "GetDistributorUp");

        try
        {
            var fromDate = UtilityHelper.GetDateTimeNowUtc();
            var toDate = fromDate.AddMinutes(-request.PeriodMinutes);

            var downloadEnts = await _dbContext.Downloads.Where(x => x.LastActivity >= toDate && x.LastActivity <= fromDate).ToListAsync();

            if (downloadEnts != null)
            {
                var finishedStatusCount = downloadEnts.Count(x => x.Status == Constants.DownloadStatusTypeFinished);

                response.AllStatusCount = downloadEnts.Count;
                response.FinishedStatusCount = finishedStatusCount;
                response.ChancesDownPercentage = (int)Math.Round((double)(100 * finishedStatusCount) / downloadEnts.Count);

                response.Feedback.InformationMessage = $"Finished status percentage aquired";
            }
            else
                response.Feedback.WarningMessage = "Failed to get Finished status percentage";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetYtdlpVersionsResponse> GetYtdlpVersions(GetYtdlpVersionsRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetYtdlpVersionsResponse>(request, "GetYtdlpVersions");

        try
        {
            var softwareVersionEnt = await _dbContext.SoftwareVersions
                    .FirstOrDefaultAsync();

            if (softwareVersionEnt != null)
            {
                response.Downloader = softwareVersionEnt.DownloaderYtdlp;
                response.Git = softwareVersionEnt.YtdlpGit;

                response.Feedback.InformationMessage = $"Ytdlp versions acquired";
            }
            else
                response.Feedback.WarningMessage = "Failed to get Ytdlp Versions";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadsDuringPeriodResponse> GetDownloadsDuringPeriod(GetDownloadsDuringPeriodRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsDuringPeriodResponse>(request, "GetDownloadsDuringPeriod");

        try
        {
            var fromDate = DateTime.Parse(request.From).SetKind(DateTimeKind.Utc);
            var toDate = DateTime.Parse(request.To).SetKind(DateTimeKind.Utc);

            var downloadsEnts = await _dbContext.Downloads.Include(x => x.DownloadSession)
                .Where(x => x.RequestedOn >= fromDate && x.RequestedOn <= toDate)
                .OrderByDescending(x => x.RequestedOn)
                .ToListAsync();

            response.Downloads.Add(ConfigureVdMapper.Map(downloadsEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDistinctErrorsDuringPeriodResponse> GetDistinctErrorsDuringPeriod(GetDistinctErrorsDuringPeriodRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDistinctErrorsDuringPeriodResponse>(request, "GetDistinctErrorsDuringPeriod");

        try
        {
            var fromDate = DateTime.Parse(request.From).SetKind(DateTimeKind.Utc);
            var toDate = DateTime.Parse(request.To).SetKind(DateTimeKind.Utc);

            var downloadsEnts = await _dbContext.Downloads
                .Where(x => x.RequestedOn >= fromDate && x.RequestedOn <= toDate
                    && !string.IsNullOrWhiteSpace(x.ErrorMessage))
                .OrderByDescending(x => x.LastActivity)
                .ToListAsync();

            downloadsEnts = downloadsEnts.DistinctBy(x => x.ErrorMessage).ToList();

            response.Errors.Add(ConfigureVdMapper.Map_ToError(downloadsEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadSessionTableHackResponse> GetDownloadSessionTableHack(GetDownloadSessionTableHackRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadSessionTableHackResponse>(request, "GetDownloadSessionTableHack");

        try
        {
            List<DownloadSession> downloadSessionEnts = await _dbContext.DownloadSessions
                .Where(x => x.Id == Guid.Parse(request.Id)).OrderByDescending(x => x.RequestedOn).ToListAsync();

            response.DownloadSessions.Add(ConfigureVdMapper.MapTable(downloadSessionEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadSessionResponse> GetDownloadSession(GetDownloadSessionRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadSessionResponse>(request, "GetDownloadSession");

        try
        {
            DownloadSession downloadSessionEnt = await _dbContext.DownloadSessions.FirstOrDefaultAsync(x => x.Id == Guid.Parse(request.Id));

            response.DownloadSession = ConfigureVdMapper.MapTable(downloadSessionEnt);
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadSessionsResponse> GetDownloadSessions(GetDownloadSessionsRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadSessionsResponse>(request, "GetDownloadSessions");

        try
        {
            List<DownloadSession> downloadSessionEnts = (request.Take == -1)
                ? await _dbContext.DownloadSessions.OrderByDescending(x => x.RequestedOn).ToListAsync()
                : await _dbContext.DownloadSessions.OrderByDescending(x => x.RequestedOn).Take(request.Take).ToListAsync();

            response.DownloadSessions.Add(ConfigureVdMapper.MapTable(downloadSessionEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadsResponse> GetDownloads(GetDownloadsRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsResponse>(request, "GetDownloads");

        try
        {
            List<Download> downloadEnts = (request.Take == -1)
                ? await _dbContext.Downloads.OrderByDescending(x => x.RequestedOn).ToListAsync()
                : await _dbContext.Downloads.OrderByDescending(x => x.RequestedOn).Take(request.Take).ToListAsync();

            response.Downloads.Add(ConfigureVdMapper.MapTable(downloadEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadsByUsernameResponse> GetDownloadsByUsername(GetDownloadsByUsernameRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsByUsernameResponse>(request, "GetDownloadsByUsername");

        try
        {
            List<Download> downloadEnts = (request.Take == -1)
                ? await _dbContext.Downloads
                    .Where(x => x.DownloadSession.Username.ToLower() == request.Username.ToLower()
                        && x.DownloadSession.Fid.ToLower() == request.ForceId.ToLower()).OrderByDescending(x => x.RequestedOn).ToListAsync()
                : await _dbContext.Downloads
                    .Where(x => x.DownloadSession.Username.ToLower() == request.Username.ToLower()
                        && x.DownloadSession.Fid.ToLower() == request.ForceId.ToLower()).OrderByDescending(x => x.RequestedOn).Take(request.Take).ToListAsync();

            response.Downloads.Add(ConfigureVdMapper.MapTable(downloadEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadsByDeploymentResponse> GetDownloadsByDeployment(GetDownloadsByDeploymentRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsByDeploymentResponse>(request, "GetDownloadsByDeployment");

        try
        {
            List<Download> downloadEnts = (request.Take == -1)
                ? await _dbContext.Downloads
                    .Where(x => x.DownloadSession.DeploymentNo.ToLower() == request.DeplNo.ToLower()
                        && x.DownloadSession.Fid.ToLower() == request.ForceId.ToLower()).OrderByDescending(x => x.RequestedOn).ToListAsync()
                : await _dbContext.Downloads
                    .Where(x => x.DownloadSession.DeploymentNo.ToLower() == request.DeplNo.ToLower()
                        && x.DownloadSession.Fid.ToLower() == request.ForceId.ToLower()).OrderByDescending(x => x.RequestedOn).Take(request.Take).ToListAsync();

            response.Downloads.Add(ConfigureVdMapper.MapTable(downloadEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadsByForceResponse> GetDownloadsByForce(GetDownloadsByForceRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsByForceResponse>(request, "GetDownloadsByForce");

        try
        {
            List<Download> downloadEnts = (request.Take == -1)
                ? await _dbContext.Downloads
                    .Where(x => x.DownloadSession.Fid.ToLower() == request.ForceId.ToLower()).OrderByDescending(x => x.RequestedOn).ToListAsync()
                : await _dbContext.Downloads
                    .Where(x => x.DownloadSession.Fid.ToLower() == request.ForceId.ToLower()).OrderByDescending(x => x.RequestedOn).Take(request.Take).ToListAsync();

            response.Downloads.Add(ConfigureVdMapper.MapTable(downloadEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadsByClientResponse> GetDownloadsByClient(GetDownloadsByClientRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsByClientResponse>(request, "GetDownloadsByClient");

        try
        {
            List<Download> downloadEnts = (request.Take == -1)
                ? await _dbContext.Downloads
                    .Where(x => x.DownloadSession.ClientName.ToLower() == request.Client.ToLower()).OrderByDescending(x => x.RequestedOn).ToListAsync()
                : await _dbContext.Downloads
                    .Where(x => x.DownloadSession.ClientName.ToLower() == request.Client.ToLower()).OrderByDescending(x => x.RequestedOn).Take(request.Take).ToListAsync();

            response.Downloads.Add(ConfigureVdMapper.MapTable(downloadEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadsBySessionResponse> GetDownloadsBySession(GetDownloadsBySessionRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadsBySessionResponse>(request, "GetDownloadsBySession");

        try
        {
            List<Download> downloadEnts = (request.Take == -1)
                ? await _dbContext.Downloads
                    .Where(x => x.DownloadSession.Id == Guid.Parse(request.SessionId)).OrderByDescending(x => x.RequestedOn).ToListAsync()
                : await _dbContext.Downloads
                    .Where(x => x.DownloadSession.Id == Guid.Parse(request.SessionId)).OrderByDescending(x => x.RequestedOn).Take(request.Take).ToListAsync();

            response.Downloads.Add(ConfigureVdMapper.MapTable(downloadEnts));
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }

    public override async Task<GetDownloadStatusDuringPeriodResponse> GetDownloadStatusDuringPeriod(GetDownloadStatusDuringPeriodRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetDownloadStatusDuringPeriodResponse>(request, "GetDownloadStatusDuringPeriod");

        try
        {
            var downloadStatusDuringPeriodEnts = await _serviceHelper.GetDownloadStatusDuringPeriodAsync(request.From, request.To);

            response.DownloadDailyStatusCounts.AddRange(ConfigureVdMapper.Map(downloadStatusDuringPeriodEnts));

            response.Feedback.ErrorMessage = downloadStatusDuringPeriodEnts == null ? $"Could not get DownloadStatusDuringPeriods" : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
}