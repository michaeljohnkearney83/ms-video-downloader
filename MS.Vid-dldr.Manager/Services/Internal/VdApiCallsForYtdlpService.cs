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
using MS.Vid_dldr.Manager.RabbitMq;
using MS.Vid_dldr.Manager.Helpers;

namespace MS.Vid_dldr.Manager.Services.Internal;

[ApiExplorerSettings(GroupName = "YtdlpService")]
public class VdApiCallsForYtdlpService : VdApiCallsForYtdlp.VdApiCallsForYtdlpBase
{
    private readonly ILogger<VdApiCallsForYtdlpService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;

    public VdApiCallsForYtdlpService(ILogger<VdApiCallsForYtdlpService> logger, AppDbContext dbContext, IMapper mapper, IAppSettings appSettings, IServiceHelper serviceHelper)
    {
        _logger = logger;
        _dbContext = dbContext;
        _mapper = mapper;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
    }

    public override async Task<SetYtdlpVersionResponse> SetYtdlpVersion(SetYtdlpVersionRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<SetYtdlpVersionResponse>(request, "SetYtdlpVersion");

        try
        {
            var existingSoftwareVersion = await _dbContext.SoftwareVersions.FirstOrDefaultAsync();

            if (existingSoftwareVersion != null)
            {
                existingSoftwareVersion.YtdlpGit = request.Version;
                await _serviceHelper.SaveAsync();
            }
            else
                response.Feedback.WarningMessage = "Not logged, no software version row logged yet";
        }
        catch (Exception ex)
        {
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
}