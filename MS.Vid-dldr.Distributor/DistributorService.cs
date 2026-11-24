using MS.Vid_dldr.Distributor.Observability;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text.Json;
using System.Text;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using MS.Vid_dldr.Manager.Protos;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MS.Vid_dldr.Distributor;

public class DistributorService : IHostedService, IDistributorService
{
    private static IAppSettings _appSettings;
    private FileSystemWatcher _fileSystemWatcher = null;
    private static HttpClient _httpClient = null;

    public DistributorService(IAppSettings appSettings, IHttpClientFactory httpClientFactory)
    {
        _appSettings = appSettings;

        _httpClient = httpClientFactory.CreateClient("ms-vid-dldr-httpClient");

        var appSettingsValue = JsonSerializer.Serialize(_appSettings.ToString());

        WriteLog(Constantss.LogStatusInfo, _appSettings.ToString());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        WriteLog(Constantss.LogStatusCall, $"StartAsync(CancellationToken cancellationToken)");

        if (!Directory.Exists(_appSettings.DownloaderShareFilePath))
        {
#if !DEBUG
            WriteLog(Constantss.LogStatusErr, $"{_appSettings.DownloaderShareFilePath} does not exist!");
            Console.ReadLine();
#else
            Directory.CreateDirectory(_appSettings.DownloaderShareFilePath);
#endif
        }

        CleanUpFiles();

        _fileSystemWatcher = new FileSystemWatcher();
        _fileSystemWatcher.Path = _appSettings.DownloaderShareFilePath;

        _fileSystemWatcher.NotifyFilter = NotifyFilters.FileName;

        _fileSystemWatcher.Renamed += fileSystemWatcher_Renamed;
        _fileSystemWatcher.Error += fileSystemWatcher_OnError;

        _fileSystemWatcher.EnableRaisingEvents = true;

        return Task.CompletedTask;
    }

    private async void fileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
    {
        WriteLog(Constantss.LogStatusCall, $"fileSystemWatcher_Renamed {Path.GetFileName(e.FullPath)}");

        if (IgnoreFile(e.Name)) return;

        await MoveFileAsync(e.FullPath, "Renamed");
    }

    private void fileSystemWatcher_OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        var exType = ex.GetType();

        var customError = exType == typeof(InternalBufferOverflowException) ? "Internal buffer overflow" : "Watched directory not accessible";

        WriteLog(Constantss.LogStatusCall, $"fileSystemWatcher_OnError {customError}");

        AttemptReconnect();
    }

    private void AttemptReconnect()
    {
        int iMaxAttempts = 120;
        int iTimeOut = 30000;
        int i = 0;

        while ((!Directory.Exists(_fileSystemWatcher.Path) || _fileSystemWatcher.EnableRaisingEvents == false) && i < iMaxAttempts)
        {
            i += 1;
            try
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                if (!Directory.Exists(_fileSystemWatcher.Path))
                {
                    WriteLog(Constantss.LogStatusWarn, $"Directory Inaccessible {_fileSystemWatcher.Path}");
                    Thread.Sleep(iTimeOut);
                }
                else
                {
                    _fileSystemWatcher.Dispose();
                    _fileSystemWatcher = null;

                    _fileSystemWatcher = new FileSystemWatcher();
                    ((System.ComponentModel.ISupportInitialize)(_fileSystemWatcher)).BeginInit();

                    _fileSystemWatcher.Path = _appSettings.DownloaderShareFilePath;

                    _fileSystemWatcher.NotifyFilter = NotifyFilters.FileName;

                    _fileSystemWatcher.Renamed += fileSystemWatcher_Renamed;
                    _fileSystemWatcher.Error += fileSystemWatcher_OnError;

                    _fileSystemWatcher.EnableRaisingEvents = true;

                    ((System.ComponentModel.ISupportInitialize)(_fileSystemWatcher)).EndInit();
                    WriteLog(Constantss.LogStatusWarn, $"Try to Restart RaisingEvents Watcher");
                }
            }
            catch (Exception ex)
            {
                WriteLog(Constantss.LogStatusEx, $"Issue trying Restart Service {ex.StackTrace}");
                _fileSystemWatcher.EnableRaisingEvents = false;
                Thread.Sleep(iTimeOut);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        WriteLog(Constantss.LogStatusCall, $"StopAsync(CancellationToken cancellationToken)");

        if (_appSettings.CallManagerApiViaRest) _httpClient.Dispose();

        return Task.CompletedTask;
    }

    public bool CleanUpFiles()
    {
        WriteLog(Constantss.LogStatusCall, $"CleanUpFiles()");

        try
        {
            var filePaths = Directory.GetFiles(_appSettings.DownloaderShareFilePath);

            foreach (string filePath in filePaths)
            {
                if (!filePath.EndsWith(_appSettings.PartFileExt))
                {
                    Task.Run(() => MoveFileAsync(filePath, "CleanUpFiles()"));
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            WriteLog(Constantss.LogStatusEx, $"{ex.ToString()}");
            return false;
        }
    }

    public async Task<bool> MoveFileAsync(string filePath, string fromEvent)
    {
        // The file system breaks the trace (the Downloader's context is not in the file), so this starts a new
        // trace; the client id tag links it to the rest of the download's spans.
        using var activity = Telemetry.Source.StartActivity("distribute file");
        activity?.SetTag(Telemetry.ClientIdTag, Path.GetFileName(filePath).Split(".")[0]);
        activity?.SetTag("vd.distributor.trigger", fromEvent);

        WriteLog(Constantss.LogStatusCall, $"MoveFileAsync(string filePath: '{filePath}', fromEvent: '{fromEvent}')");

        try
        {
            var prevFilenameFull = Path.GetFileName(filePath);
            var prevFilenameFullSplit = prevFilenameFull.Split(".");
            var clientId = prevFilenameFullSplit[0];
            var prevFileExtension = prevFilenameFullSplit[1];

            var downloadFileInfo = await CallGetRequestedDownloadFileInformationAsync(clientId, _appSettings.CallManagerApiViaRest);
            if (downloadFileInfo == null || downloadFileInfo.Feedback.ErrorMessage.StartsWith("Could not find Download by ClientId:"))
            {
                WriteLog(Constantss.LogStatusWarn, $"CallGetRequestedDownloadFileInformationAsync returned: 404 or ClientId {clientId} couldnt be found via the Manager!");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(downloadFileInfo.Feedback.ErrorMessage))
            {
                WriteLog(Constantss.LogStatusErr, $"CallGetRequestedDownloadFileInformationAsync returned: \"{downloadFileInfo?.Feedback.ErrorMessage}\"");
                await DeleteFileFromShareFolderAndUpdateStatusAsync(clientId, filePath, $"An error happened while downloading {downloadFileInfo.Feedback.ErrorMessage}");
                return false;
            }

            if (!Directory.Exists(downloadFileInfo.RequestedDownloadFolder))
            {
                WriteLog(Constantss.LogStatusErr, $"Folder downloadFileInfo.RequestedDownloadFolder: {downloadFileInfo.RequestedDownloadFolder} doesnt exist!");
                await DeleteFileFromShareFolderAndUpdateStatusAsync(clientId, filePath, $"Requested folder doesnt exist {downloadFileInfo.RequestedDownloadFolder}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(downloadFileInfo.RequestedDownloadName)) downloadFileInfo.RequestedDownloadName = clientId;
            var requestedFilePath = $"{downloadFileInfo.RequestedDownloadFolder}//{downloadFileInfo.RequestedDownloadName}.{prevFileExtension}";

            try
            {
                if (!File.Exists(filePath))
                {
                    WriteLog(Constantss.LogStatusErr, $"File -FROM- doesnt exists before File.Move(): filePath: '{filePath}'");
                    await DeleteFileFromShareFolderAndUpdateStatusAsync(clientId, filePath, $"Share file doesnt exist {filePath}");
                    return false;
                }

                if (File.Exists(requestedFilePath))
                    WriteLog(Constantss.LogStatusErr, $"File -TO- exists before File.Move(), possible dup rename event raised: requestedFilePath: {requestedFilePath}");

                if (!DistributeFileToDeploymentFolder(filePath, requestedFilePath))
                {
                    WriteLog(Constantss.LogStatusErr, $"Failed to move after {_appSettings.DistributeFileToDeploymentFolderMaxRetry} retries & waiting {_appSettings.DistributeFileToDeploymentFolderWait}.");
                    await DeleteFileFromShareFolderAndUpdateStatusAsync(clientId, filePath, $"Failed to distribute file filePath {filePath} requestedFilePath: {requestedFilePath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                WriteLog(Constantss.LogStatusEx, $"{ex.ToString()}: \r\n[VALUES] filePath: '{filePath}', requestedFilePath: '{requestedFilePath}'");
                await DeleteFileFromShareFolderAndUpdateStatusAsync(clientId, filePath, $"Exception throw {ex.ToString()}");
                return false;
            }

            var updateDownloadProgressDistributedResp = await CallUpdateDownloadProgressDistributedAsync(clientId, _appSettings.CallManagerApiViaRest);

            if (updateDownloadProgressDistributedResp == null || !string.IsNullOrWhiteSpace(updateDownloadProgressDistributedResp.Feedback?.ErrorMessage))
            {
                WriteLog(Constantss.LogStatusErr, $"CallUpdateDownloadProgressDistributedAsync returned: null or '\"{updateDownloadProgressDistributedResp?.Feedback?.ErrorMessage}\"'");
                await DeleteFileFromShareFolderAndUpdateStatusAsync(clientId, filePath, $"Couldnt get file information from distributed status set {updateDownloadProgressDistributedResp?.Feedback?.ErrorMessage}");
                return false;
            }
            WriteLog(Constantss.LogStatusInfo, $"Processed: filePath: '{filePath}', requestedFilePath: '{requestedFilePath}'");

            return true;
        }
        catch (Exception ex)
        {
            WriteLog(Constantss.LogStatusEx, $"{ex.ToString()}");
            return false;
        }
    }
    public async Task<GetRequestedDownloadFileInformationResponse> CallGetRequestedDownloadFileInformationAsync(string clientId, bool isRestCall)
    {
        WriteLog(Constantss.LogStatusCall, $"CallGetRequestedDownloadFileInformationAsync(string clientId: {clientId}, bool isRestCall: {isRestCall})");

        GetRequestedDownloadFileInformationResponse result = null;

        try
        {
            var username = _appSettings.DistributorUsername;

            if (!isRestCall)
            {
                using var apiChannel = GrpcChannel.ForAddress(_appSettings.ManagerApiAddressGrpc);
                var clientGrpc = new VdApiCallsForDistributor.VdApiCallsForDistributorClient(apiChannel);

                result = await clientGrpc.GetRequestedDownloadFileInformationAsync(
                    new GetRequestedDownloadFileInformationRequest
                    {
                        Username = username,
                        ClientId = clientId
                    });
            }
            else
            {
                var managerApiCall = _appSettings.ManagerApiEndpointGetRequestedDownloadFileInformation
                    .Replace("{username}", username)
                    .Replace("{client_id}", clientId);
                var response = await _httpClient.GetAsync(managerApiCall);
                if (response.IsSuccessStatusCode)
                    result = response.Content.ReadAsAsync<GetRequestedDownloadFileInformationResponse>().Result;

                WriteLog(Constantss.LogStatusInfo, $"response.StatusCode: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            WriteLog(Constantss.LogStatusEx, $"{ex.ToString()}");
        }

        return result;
    }

    public async Task<UpdateDownloadProgressDistributedResponse> CallUpdateDownloadProgressDistributedAsync(string clientId, bool isRestCall)
    {
        WriteLog(Constantss.LogStatusCall, $"CallUpdateDownloadProgressDistributedAsync(string clientId: {clientId}, bool isRestCall: {isRestCall})");

        UpdateDownloadProgressDistributedResponse result = null;

        try
        {
            if (!isRestCall)
            {
                using var apiChannel = GrpcChannel.ForAddress(_appSettings.ManagerApiAddressGrpc);
                var clientGrpc = new VdApiCallsForDistributor.VdApiCallsForDistributorClient(apiChannel);

                result = await clientGrpc.UpdateDownloadProgressDistributedAsync(
                    new UpdateDownloadProgressDistributedRequest
                    {
                        ClientId = clientId,
                    });
            }
            else
            {
                var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
                var managerApiCall = _appSettings.ManagerApiEndpointUpdateDownloadProgressDistributed
                    .Replace("{client_id}", clientId);
                var response = await _httpClient.PutAsync(managerApiCall, httpContent);
                if (response.IsSuccessStatusCode)
                    result = response.Content.ReadAsAsync<UpdateDownloadProgressDistributedResponse>().Result;

                WriteLog(Constantss.LogStatusInfo, $"response.StatusCode: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            WriteLog(Constantss.LogStatusEx, $"{ex.ToString()}");
        }

        return result;
    }

    public async Task<UpdateDownloadProgressDeletedResponse> CallUpdateDownloadProgressDeletedAsync(string clientId, string reason, bool isRestCall)
    {
        WriteLog(Constantss.LogStatusCall, $"CallUpdateDownloadProgressDeletedAsync(string clientId: {clientId}, string reason: {reason}, bool isRestCall: {isRestCall})");

        UpdateDownloadProgressDeletedResponse result = null;

        try
        {
            reason = Sanitise(reason);

            if (!isRestCall)
            {
                using var apiChannel = GrpcChannel.ForAddress(_appSettings.ManagerApiAddressGrpc);
                var clientGrpc = new VdApiCallsForDistributor.VdApiCallsForDistributorClient(apiChannel);

                result = await clientGrpc.UpdateDownloadProgressDeletedAsync(
                    new UpdateDownloadProgressDeletedRequest
                    {
                        ClientId = clientId,
                        Reason = reason,
                    });
            }
            else
            {
                var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
                var managerApiCall = _appSettings.ManagerApiEndpointUpdateDownloadProgressDeleted
                    .Replace("{client_id}", clientId)
                    .Replace("{reason}", $"Distributor error {reason}");
                var response = await _httpClient.PutAsync(managerApiCall, httpContent);
                if (response.IsSuccessStatusCode)
                    result = response.Content.ReadAsAsync<UpdateDownloadProgressDeletedResponse>().Result;

                WriteLog(Constantss.LogStatusInfo, $"response.StatusCode: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            WriteLog(Constantss.LogStatusEx, $"{ex.ToString()}");
        }

        return result;
    }

    private bool DistributeFileToDeploymentFolder(string filePath, string requestedFilePath)
    {
        int retryCount = _appSettings.DistributeFileToDeploymentFolderMaxRetry;

        while (retryCount > 0)
        {
            try
            {
                File.Move(filePath, requestedFilePath, true);
                retryCount = 0;

                return true;
            }
            catch (IOException)
            {
                if (!Retry("IOException")) return false;
            }
            catch (UnauthorizedAccessException)
            {
                if (!Retry("UnauthorizedAccessException")) return false;
            }
        }

        return false;

        bool Retry(string exceptionType)
        {
            WriteLog(Constantss.LogStatusWarn, $"DistributeFileToDeploymentFolder {exceptionType} retryCount: {retryCount}");

            retryCount--;

            if (retryCount <= 0) return false;

            Thread.Sleep(_appSettings.DistributeFileToDeploymentFolderWait);

            return true;
        }
    }

    private async Task DeleteFileFromShareFolderAndUpdateStatusAsync(string clientId, string filePath, string reason)
    {
        if (!DeleteFileFromShareFolder(filePath))
        {
            WriteLog(Constantss.LogStatusErr, $"Failed to delete after {_appSettings.DistributeFileToDeploymentFolderMaxRetry} retries & waiting {_appSettings.DistributeFileToDeploymentFolderWait}.");
        }
        else
        {
            WriteLog(Constantss.LogStatusInfo, $"Deleted file");
            await CallUpdateDownloadProgressDeletedAsync(clientId, reason, _appSettings.CallManagerApiViaRest);
        }
    }

    private bool DeleteFileFromShareFolder(string filePath)
    {
        if (!File.Exists(filePath)) return true;

        int retryCount = _appSettings.DistributeFileToDeploymentFolderMaxRetry;

        while (retryCount > 0)
        {
            try
            {
                File.Delete(filePath);
                retryCount = 0;

                return true;
            }
            catch (IOException)
            {
                if (!Retry("IOException")) return false;
            }
            catch (UnauthorizedAccessException)
            {
                if (!Retry("UnauthorizedAccessException")) return false;
            }
        }

        return false;

        bool Retry(string exceptionType)
        {
            Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[WARN] DeleteFileFromShareFolder {exceptionType} retryCount: {retryCount}");

            retryCount--;

            if (retryCount <= 0) return false;

            Thread.Sleep(_appSettings.DistributeFileToDeploymentFolderWait);

            return true;
        }
    }

    internal bool IgnoreFile(string name)
    {
        WriteLog(Constantss.LogStatusCall, $"IgnoreFile(string name: {name})");

        if (name.Split(".").Count() != 2) return true;

        var formatToIgnore = _appSettings.FormatTypeWhitelist ?? new List<string>();

        return (!Guid.TryParse(name.Split(".")[0], out _)
        || name.EndsWith(_appSettings.PartFileExt)
        || name.EndsWith(_appSettings.TempMp4FileExt)
        || name.EndsWith(_appSettings.M4aFileExt)
        || name.EndsWith(_appSettings.YtdlpFileExt)
        || name.EndsWith(_appSettings.InfoFileExt)
        || name.Contains(_appSettings.FragPartFileContains)
        || IsBbcIgnore(name)
        || IsFacebookIgnore(name)
        || formatToIgnore.Any(x => name.EndsWith(x)));
    }

    internal bool IsBbcIgnore(string filename)
    {
        var filenameSplit = filename.Split(".");

        var ext = filenameSplit[1];

        return !string.IsNullOrWhiteSpace(ext)
                    ? (ext.Length == 18 && ext.StartsWith("f") && ext.EndsWith("v"))
                    : false;
    }

    internal bool IsFacebookIgnore(string filename)
    {
        var filenameSplit = filename.Split(".");

        var ext = filenameSplit[1];

        return !string.IsNullOrWhiteSpace(ext)
                    ? (ext.StartsWith("fmf_cloudfront-video="))
                    : false;
    }

    public string Sanitise(string url)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        byte[] bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(url.ToLower());
        url = System.Text.Encoding.ASCII.GetString(bytes);

        url = System.Text.RegularExpressions.Regex.Replace(url, @"[^a-z0-9\s-]", "_");
        url = System.Text.RegularExpressions.Regex.Replace(url, @"\s+", "_").Trim();
        url = System.Text.RegularExpressions.Regex.Replace(url, @"\s", "-");

        return url;
    }

    private void WriteLog(string status, string line)
    {
        Telemetry.Log(status, line);
    }
}