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
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Protos.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Vid_dldr.Manager.Health;

namespace MS.Vid_dldr.Manager.Services;

[ApiExplorerSettings(GroupName = "GenericService")]
public class VdApiCallsForGenericService : VdApiCallsForGeneric.VdApiCallsForGenericBase
{
    private readonly ILogger<VdApiCallsForGenericService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;
    private readonly HealthCheckService _healthCheckService;

    public VdApiCallsForGenericService(ILogger<VdApiCallsForGenericService> logger, AppDbContext dbContext, IMapper mapper, IAppSettings appSettings, IServiceHelper serviceHelper,
        HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
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
            // Same checks as /health/ready: up unless Postgres or RabbitMQ is down. A missing worker is reported in
            // the warning but doesn't make the Manager "down" (requests still queue).
            var report = await _healthCheckService.CheckHealthAsync(HealthSetup.IsReady, context.CancellationToken);
            response.IsUp = report.Status == HealthStatus.Unhealthy ? 0 : 1;
            response.Feedback.InformationMessage = $"Service manager is up: {report.Status}";
            response.Feedback.WarningMessage = string.Join("; ", report.Entries
                .Where(e => e.Value.Status != HealthStatus.Healthy)
                .Select(e => $"{e.Key}: {e.Value.Status} - {e.Value.Description}"));
        }
        catch (Exception ex)
        {
            response.IsUp = 0;
            response.Feedback.Exception = ex.ToString();
        }

        return response;
    }
}