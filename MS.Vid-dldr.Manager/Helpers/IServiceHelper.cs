using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Entities.Scripts;

namespace MS.Vid_dldr.Manager.Helpers;

public interface IServiceHelper
{
    T SetupMethod<T>(Object request, string? methodSignature = null);

    string TranslateStatus(string value);

    Task<bool> RetryAnalysisDownloadAsync(Download downloadEnt);

    Task<List<DownloadStatusDuringPeriod>> GetDownloadStatusDuringPeriodAsync(string startDate, string endString);

    Task<string> GetDatabaseVersionAsync();

    Task<string?> GetLastDbMigrationAsync();

    Task<bool> SaveAsync(int? expectedRowsAffected = null);
}