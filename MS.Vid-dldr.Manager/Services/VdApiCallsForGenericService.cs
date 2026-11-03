using Grpc.Core;
using AutoMapper;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf.Collections;
using Microsoft.AspNetCore.Mvc;
using MS.Vid_dldr.Manager.Protos.FromGeneric;
using MS.Vid_dldr.Manager.Data;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.RabbitMq;
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Protos.Common;

namespace MS.Vid_dldr.Manager.Services;

[ApiExplorerSettings(GroupName = "GenericService")]
public class VdApiCallsForGenericService : VdApiCallsForGeneric.VdApiCallsForGenericBase
{
    private readonly ILogger<VdApiCallsForGenericService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;

    public VdApiCallsForGenericService(ILogger<VdApiCallsForGenericService> logger, AppDbContext dbContext, IMapper mapper, IAppSettings appSettings, IServiceHelper serviceHelper)
    {
        _logger = logger;
        _dbContext = dbContext;
        _mapper = mapper;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
    }

    public override async Task<IsServiceUpResponse> IsServiceUp(IsServiceUpRequest request, ServerCallContext context)
    {
        var response = _serviceHelper.SetupMethod<IsServiceUpResponse>(request, "IsServiceUp");

        try
        {
            response.IsUp = 1;
            response.Feedback.InformationMessage = "Service manager is up";
        }
        catch (Exception ex)
        {
            response.IsUp = 0;
            response.Feedback.Exception = ex.ToString();
        }

        return await Task.FromResult(response);
    }
}