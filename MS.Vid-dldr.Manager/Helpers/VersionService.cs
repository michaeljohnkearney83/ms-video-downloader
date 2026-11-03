using System.Runtime.InteropServices;
using Google.Type;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.RabbitMq;

namespace MS.Vid_dldr.Manager.Helpers;

public class VersionService : IVersionService
{
    private readonly ILogger<VersionService> _logger;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;

    public VersionService(ILogger<VersionService> logger, IAppSettings appSettings, IServiceHelper serviceHelper)
    {
        _logger = logger;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
    }

    public async void InitiateCollectionOfVersions(System.DateTime? requested = null)
    {
        _logger.LogInformation($"{Constants.MsNoticeLogPrefix}InitiateCollectionOfVersions()");

        var rmqProducer = new RMQProducer(_appSettings.ChannelHostName, _appSettings.ChannelPortNumber, _appSettings.QueueRequestDownload);

        var setVersionsDto = new SetVersionsDto
        {
            ManagerApp = UtilityHelper.GetVersion(),
            ManagerFramework = RuntimeInformation.FrameworkDescription,
            ManagerDb = await _serviceHelper.GetDatabaseVersionAsync(),
            ManagerDbLastMigration = await _serviceHelper.GetLastDbMigrationAsync(),
            Requested = requested == null ? UtilityHelper.GetDateTimeNowUtc() : requested.Value,
        };

        rmqProducer.Publish(setVersionsDto.ToString());
    }
}