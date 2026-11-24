namespace MS.Vid_dldr.Distributor;

public interface IAppSettings
{
    string BuildType { get; set; }
    string ManagerApiAddressGrpc { get; set; }
    Uri ManagerApiAddressRestConfigBaseUrl { get; set; }
    TimeSpan ManagerApiAddressRestConfigTimeout { get; set; }
    bool CallManagerApiViaRest { get; set; }
    string DownloaderShareFilePath { get; set; }
    string PartFileExt { get; set; }
    string TempMp4FileExt { get; set; }
    string M4aFileExt { get; set; }
    string YtdlpFileExt { get; set; }
    string InfoFileExt { get; set; }
    List<string> FormatTypeWhitelist { get; set; }
    string FragPartFileContains { get; set; }
    int DistributeFileToDeploymentFolderMaxRetry { get; set; }
    int DistributeFileToDeploymentFolderWait { get; set; }
    string HomeDriveLocation { get; set; }
    string DistributorUsername { get; set; }
    string ManagerApiEndpointGetRequestedDownloadFileInformation { get; set; }
    string ManagerApiEndpointUpdateDownloadProgressDistributed { get; set; }
    string ManagerApiEndpointUpdateDownloadProgressDeleted { get; set; }

    string ToString();
}