using System.Runtime.InteropServices;
using Google.Type;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Messaging;

namespace MS.Vid_dldr.Manager.Helpers;

public class VersionService : IVersionService
{
    private readonly ILogger<VersionService> _logger;
    private readonly IAppSettings _appSettings;
    private readonly IServiceHelper _serviceHelper;
    private readonly IDownloadRequestPublisher _publisher;

    public VersionService(ILogger<VersionService> logger, IAppSettings appSettings, IServiceHelper serviceHelper,
        IDownloadRequestPublisher publisher)
    {
        _logger = logger;
        _appSettings = appSettings;
        _serviceHelper = serviceHelper;
        _publisher = publisher;
    }

    public async Task InitiateCollectionOfVersionsAsync(System.DateTime? requested = null)
    {
        _logger.LogInformation($"{Constants.MsNoticeLogPrefix}InitiateCollectionOfVersions()");

        var setVersionsDto = new SetVersionsDto
        {
            ManagerApp = UtilityHelper.GetVersion(),
            ManagerFramework = RuntimeInformation.FrameworkDescription,
            ManagerDb = await _serviceHelper.GetDatabaseVersionAsync(),
            ManagerDbLastMigration = await _serviceHelper.GetLastDbMigrationAsync(),
            Requested = requested == null ? UtilityHelper.GetDateTimeNowUtc() : requested.Value,
        };

        await _publisher.SendAsync(setVersionsDto);
    }
}