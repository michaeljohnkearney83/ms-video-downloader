using MS.Vid_dldr.Manager.Protos;

namespace MS.Vid_dldr.Distributor;

public interface IDistributorService
{
    bool CleanUpFiles();
    Task<bool> MoveFileAsync(string filePath, string fromEvent);
    Task<GetRequestedDownloadFileInformationResponse> CallGetRequestedDownloadFileInformationAsync(string clientId, bool isRestCall);
    Task<UpdateDownloadProgressDistributedResponse> CallUpdateDownloadProgressDistributedAsync(string clientId, bool isRestCall);
    Task<UpdateDownloadProgressDeletedResponse> CallUpdateDownloadProgressDeletedAsync(string clientId, string reason, bool isRestCall);
}