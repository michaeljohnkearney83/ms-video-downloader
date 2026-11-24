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
using MS.Vid_dldr.Manager.Helpers;

namespace MS.Vid_dldr.Manager.Services.Internal;

[ApiExplorerSettings(GroupName = "DistributorService")]
public class VdApiCallsForDistributorService : VdApiCallsForDistributor.VdApiCallsForDistributorBase
{
    private readonly ILogger<VdApiCallsForDistributorService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;

    public VdApiCallsForDistributorService(ILogger<VdApiCallsForDistributorService> logger, AppDbContext dbContext, IMapper mapper, IAppSettings appSettings, IServiceHelper serviceHelper)
    {
        _logger = logger;
        _dbContext = dbContext;
        _mapper = mapper;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
    }

    public override async Task<GetRequestedDownloadFileInformationResponse> GetRequestedDownloadFileInformation(GetRequestedDownloadFileInformationRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<GetRequestedDownloadFileInformationResponse>(request, "GetRequestedDownloadFileInformation");

        try
        {
            var downloadEnt = (Download)null;
            var retryCount = 0;

            while (retryCount < _appSettings.WaitForYtdlpToFinishRetryMaxCount - 1)
            {
                downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.ClientId == Guid.Parse(request.ClientId) && x.DownloadSession.Username.ToLower() == request.Username.ToLower());

                if (downloadEnt != null && downloadEnt.Status == Constants.DownloadStatusTypeFinished) break;

                if (++retryCount >= _appSettings.WaitForYtdlpToFinishRetryMaxCount)
                {
                    response.Feedback.ErrorMessage = $"No Download status Finished after {retryCount} retries & {_appSettings.WaitForYtdlpToFinishRetryWait} interval";
                    break;
                }

                Thread.Sleep(_appSettings.WaitForYtdlpToFinishRetryWait);
            }

            if (downloadEnt != null && string.IsNullOrWhiteSpace(response.Feedback.ErrorMessage))
            {
                downloadEnt.LastActivity = UtilityHelper.GetDateTimeNowUtc(); ;
                downloadEnt.Status = Constants.DownloadStatusTypeDistributedLookup;

                await _serviceHelper.SaveAsync();
            }
            else response.Feedback.ErrorMessage = $"Could not find Download by ClientId: {request.ClientId}";

            response.RequestedDownloadFolder = downloadEnt != null ? downloadEnt.RequestedDownloadFolder : "";
            response.RequestedDownloadName = downloadEnt != null ? downloadEnt.RequestedDownloadName : "";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<UpdateDownloadProgressDistributedResponse> UpdateDownloadProgressDistributed(UpdateDownloadProgressDistributedRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<UpdateDownloadProgressDistributedResponse>(request, "UpdateDownloadProgressDistributed");

        try
        {
            var downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.ClientId == Guid.Parse(request.ClientId));
            if (downloadEnt != null)
            {
                downloadEnt.LastActivity = UtilityHelper.GetDateTimeNowUtc();
                downloadEnt.Status = Constants.DownloadStatusTypeDistributed;
                downloadEnt.ErrorMessage = null;

                await _serviceHelper.SaveAsync();
            }
            else response.Feedback.ErrorMessage = $"Could not find Download by ClientId: {request.ClientId}";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
    public override async Task<UpdateDownloadProgressDeletedResponse> UpdateDownloadProgressDeleted(UpdateDownloadProgressDeletedRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<UpdateDownloadProgressDeletedResponse>(request, "UpdateDownloadProgressDeleted");

        try
        {
            var downloadEnt = await _dbContext.Downloads.FirstOrDefaultAsync(x => x.ClientId == Guid.Parse(request.ClientId));
            if (downloadEnt != null)
            {
                downloadEnt.LastActivity = UtilityHelper.GetDateTimeNowUtc();
                downloadEnt.Status = Constants.DownloadStatusTypeDeleted;
                downloadEnt.ErrorMessage = request.Reason;

                await _serviceHelper.SaveAsync();
            }
            else response.Feedback.ErrorMessage = $"Could not find Download by ClientId: {request.ClientId}";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
}