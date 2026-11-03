using System.Text.Json;
using AutoMapper;
using Grpc.Core;
using MS.Vid_dldr.Manager.Dtos;
using Microsoft.EntityFrameworkCore;
using MS.Vid_dldr.Manager.Data;
using System.Reflection;
using MS.Vid_dldr.Manager;
using MS.Vid_dldr.Manager.Protos;
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Protos.Common;
using MS.Vid_dldr.Manager.Entities.Scripts;
using MS.Vid_dldr.Manager.RabbitMq;
using MS.Vid_dldr.Manager.Entities;
using System.Text;
using Microsoft.Extensions.ObjectPool;
using Npgsql;

namespace MS.Vid_dldr.Manager.Helpers;

public class ServiceHelper : IServiceHelper
{
    private readonly ILogger<ServiceHelper> _logger;
    private readonly IMapper _mapper;
    private readonly IAppSettings _appSettings;
    private readonly AppDbContext _dbContext;
    private readonly RMQProducer _rmqProducer;

    public ServiceHelper(ILogger<ServiceHelper> logger, IMapper mapper, IAppSettings appSettings, AppDbContext dbContext)
    {
        _logger = logger;
        _mapper = mapper;
        _appSettings = appSettings;
        _dbContext = dbContext;

        _rmqProducer = new RMQProducer(_appSettings.ChannelHostName, _appSettings.ChannelPortNumber, _appSettings.QueueRequestDownload);
    }

    public T SetupMethod<T>(Object request, string? methodName = null)
    {
        var requestJsonStr = request != null ? JsonSerializer.Serialize(request) : "MISSING";
        if (methodName != null) _logger.LogInformation($"{Constants.MsNoticeLogPrefix}{methodName}({methodName}Request {requestJsonStr}");

        var response = (T)Activator.CreateInstance<T>();

        var feedback = new Feedback();
        if (request == null)
            feedback.ErrorMessage = "You must supply a valid request object";
        string requestJsonStringValue = requestJsonStr;
        PropertyInfo requestJsonStringPropInfo = feedback.GetType().GetProperty("RequestJsonString");
        if (requestJsonStringPropInfo == null) throw new Exception("Response object doesnt no contain 'RequestJsonString' property");
        requestJsonStringPropInfo.SetValue(feedback, Convert.ChangeType(requestJsonStringValue, requestJsonStringPropInfo.PropertyType), null);

        var feedbackPropInfo = response.GetType().GetProperty("Feedback");
        if (feedbackPropInfo == null) throw new Exception("Response object doesnt no contain 'Feedback' property");
        feedbackPropInfo.SetValue(response, Convert.ChangeType(feedback, feedbackPropInfo.PropertyType), null);

        return response;
    }

    public string TranslateStatus(string value)
    {
        var status = "NONE";

        switch (value.ToLower())
        {
            case null:
            case "":
                status = Constants.DownloadStatusTypeNone;
                break;
            case "requested":
                status = Constants.DownloadStatusTypeRequested;
                break;
            case "queued":
                status = Constants.DownloadStatusTypeQueued;
                break;
            case "analysis":
                status = Constants.DownloadStatusTypeAnalysis;
                break;
            case "downloading":
                status = Constants.DownloadStatusTypeDownloading;
                break;
            case "finished":
                status = Constants.DownloadStatusTypeFinished;
                break;
            case "errored":
                status = Constants.DownloadStatusTypeErrored;
                break;
        }

        return status;
    }

    public bool RetryAnalysisDownload(Download downloadEnt)
    {
        _logger.LogInformation($"RetryAnalysisDownload(Download downloadEnt: {JsonSerializer.Serialize(downloadEnt)}");

        bool result = false;

        try
        {
            var analysisRequestDto = _mapper.Map<AnalysisRequestDto>(downloadEnt);

            _logger.LogInformation($"{Constants.MsNoticeLogPrefix}{analysisRequestDto.ToString()}");

            analysisRequestDto.IsRetry = true;

            _rmqProducer.Publish(analysisRequestDto.ToString());

            result = true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"{Constants.MsNoticeLogPrefix}{ex.ToString()}");
        }

        return result;
    }

    public async Task<List<DownloadStatusDuringPeriod>> GetDownloadStatusDuringPeriodAsync(string startDate, string endString)
    {
        var sqlText = System.IO.File.ReadAllText("./Data/Scripts/GetDownloadStatusDuringPeriod.sql");
        var sql = string.Format(sqlText, startDate, endString);

        return await _dbContext.DownloadStatusDuringPeriods.FromSqlRaw<DownloadStatusDuringPeriod>(sql).ToListAsync();
    }

    public async Task<string> GetDatabaseVersionAsync()
    {
        var version = "";

        NpgsqlConnection npgSqlConnection = new NpgsqlConnection(_appSettings.ConnectionStringsDownloadsDb);
        npgSqlConnection.Open();
        NpgsqlCommand command = new NpgsqlCommand("SELECT setting FROM pg_settings WHERE name = 'server_version'", npgSqlConnection);

        var result = await command.ExecuteScalarAsync();

        if (result != null)
            version = result.ToString();

        npgSqlConnection.Close();

        return version;
    }

    public async Task<string?> GetLastDbMigrationAsync()
    {
        var migrations = await _dbContext.Database.GetAppliedMigrationsAsync();

        return migrations.LastOrDefault();
    }

    public async Task<bool> SaveAsync(int? expectedRowsAffected = null)
    {
        var rowsAffected = await _dbContext.SaveChangesAsync();

        return expectedRowsAffected == null
        ? (rowsAffected != 0)
        : (rowsAffected == expectedRowsAffected.Value);
    }
}