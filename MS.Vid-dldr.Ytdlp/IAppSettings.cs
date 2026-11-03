namespace MS.Vid_dldr.Ytdlp;

public interface IAppSettings
{
    string BuildType { get; set; }
    string ManagerApiAddressGrpc { get; set; }
    Uri ManagerApiAddressRestConfigBaseUrl { get; set; }
    TimeSpan ManagerApiAddressRestConfigTimeout { get; set; }
    bool CallManagerApiViaRest { get; set; }
    int DistributeFileToDeploymentFolderMaxRetry { get; set; }
    int DistributeFileToDeploymentFolderWait { get; set; }
    string YtdlpGitUrl { get; set; }
    string QuerySelectorString { get; set; }
    string StringReplaceFoundVersion { get; set; }
    int PollInterval { get; set; }  //2 min = 120000 ms / 12 h = 43200000 ms
    string ManagerApiEndpointUpdateVersion { get; set; }

    string ToString();
}