using Microsoft.Extensions.Configuration;

namespace MS.Vid_dldr.Ytdlp;

public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        BuildType = configuration["BuildType"];
        ManagerApiAddressGrpc = configuration["ManagerApiAddressGrpc"];
        ManagerApiAddressRestConfigBaseUrl = new Uri(configuration["ManagerApiRestConfig:BaseUrl"]);
        ManagerApiAddressRestConfigTimeout = TimeSpan.Parse(configuration["ManagerApiRestConfig:Timeout"]);
        CallManagerApiViaRest = Convert.ToBoolean(configuration["CallManagerApiViaRest"]);
        DistributeFileToDeploymentFolderMaxRetry = Convert.ToInt32(configuration["DistributeFileToDeploymentFolderMaxRetry"]);
        DistributeFileToDeploymentFolderWait = Convert.ToInt32(configuration["DistributeFileToDeploymentFolderWait"]);
        YtdlpGitUrl = configuration["YtdlpGitUrl"];
        QuerySelectorString = configuration["QuerySelectorString"];
        StringReplaceFoundVersion = configuration["StringReplaceFoundVersion"];
        PollInterval = Convert.ToInt32(configuration["PollInterval"]);
        ManagerApiEndpointUpdateVersion = configuration["ManagerApiEndpointUpdateVersion"];
    }

    public string BuildType { get; set; }
    public string ManagerApiAddressGrpc { get; set; }
    public Uri ManagerApiAddressRestConfigBaseUrl { get; set; }
    public TimeSpan ManagerApiAddressRestConfigTimeout { get; set; }
    public bool CallManagerApiViaRest { get; set; }
    public int DistributeFileToDeploymentFolderMaxRetry { get; set; }
    public int DistributeFileToDeploymentFolderWait { get; set; }
    public string YtdlpGitUrl { get; set; }
    public string QuerySelectorString { get; set; }
    public string StringReplaceFoundVersion { get; set; }
    public int PollInterval { get; set; }
    public string ManagerApiEndpointUpdateVersion { get; set; }

    public override string ToString()
    {
        return $"BuildType: {BuildType}, " +
            $"ManagerApiAddressGrpc: {ManagerApiAddressGrpc}, " +
            $"ManagerApiAddressRestConfigBaseUrl: {ManagerApiAddressRestConfigBaseUrl.ToString()}, " +
            $"ManagerApiAddressRestConfigTimeout: {ManagerApiAddressRestConfigTimeout.ToString()}, " +
            $"CallManagerApiViaRest: {CallManagerApiViaRest}, " +
            $"DistributeFileToDeploymentFolderMaxRetry: {DistributeFileToDeploymentFolderMaxRetry}, " +
            $"DistributeFileToDeploymentFolderWait: {DistributeFileToDeploymentFolderWait}, " +
            $"YtdlpGitUrl: {YtdlpGitUrl}, " +
            $"QuerySelectorString: {QuerySelectorString}, " +
            $"StringReplaceFoundVersion: {StringReplaceFoundVersion}, " +
            $"PollInterval: {PollInterval.ToString()}, " +
            $"ManagerApiEndpointUpdateVersion: {ManagerApiEndpointUpdateVersion}";
    }
}