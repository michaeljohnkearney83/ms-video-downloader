using Microsoft.Extensions.Configuration;

namespace MS.Vid_dldr.Distributor;

public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        BuildType = configuration["BuildType"];
        ManagerApiAddressGrpc = configuration["ManagerApiAddressGrpc"];
        ManagerApiAddressRestConfigBaseUrl = new Uri(configuration["ManagerApiRestConfig:BaseUrl"]);
        ManagerApiAddressRestConfigTimeout = TimeSpan.Parse(configuration["ManagerApiRestConfig:Timeout"]);
        CallManagerApiViaRest = Convert.ToBoolean(configuration["CallManagerApiViaRest"]);
        DownloaderShareFilePath = configuration["DownloaderShareFilePath"];
        PartFileExt = configuration["PartFileExt"];
        TempMp4FileExt = configuration["TempMp4FileExt"];
        M4aFileExt = configuration["M4aFileExt"];
        YtdlpFileExt = configuration["YtdlpFileExt"];
        InfoFileExt = configuration["InfoFileExt"];
        FragPartFileContains = configuration["FragPartFileContains"];
        FormatTypeWhitelist = configuration.GetSection("FormatTypeWhitelist").Get<Dictionary<string, bool>>()?.Where(x => x.Value).Select(x => x.Key).ToList() ?? new List<string>();
        DistributeFileToDeploymentFolderMaxRetry = Convert.ToInt32(configuration["DistributeFileToDeploymentFolderMaxRetry"]);
        DistributeFileToDeploymentFolderWait = Convert.ToInt32(configuration["DistributeFileToDeploymentFolderWait"]);
        HomeDriveLocation = configuration["HomeDriveLocation"];
        ManagerApiEndpointGetRequestedDownloadFileInformation = configuration["ManagerApiEndpointGetRequestedDownloadFileInformation"];
        ManagerApiEndpointUpdateDownloadProgressDistributed = configuration["ManagerApiEndpointUpdateDownloadProgressDistributed"];
        ManagerApiEndpointUpdateDownloadProgressDeleted = configuration["ManagerApiEndpointUpdateDownloadProgressDeleted"];
    }

    public string BuildType { get; set; }
    public string ManagerApiAddressGrpc { get; set; }
    public Uri ManagerApiAddressRestConfigBaseUrl { get; set; }
    public TimeSpan ManagerApiAddressRestConfigTimeout { get; set; }
    public bool CallManagerApiViaRest { get; set; }
    public string DownloaderShareFilePath { get; set; }
    public string PartFileExt { get; set; }
    public string TempMp4FileExt { get; set; }
    public string M4aFileExt { get; set; }
    public string YtdlpFileExt { get; set; }
    public string InfoFileExt { get; set; }
    public string FragPartFileContains { get; set; }
    public List<string> FormatTypeWhitelist { get; set; }
    public int DistributeFileToDeploymentFolderMaxRetry { get; set; }
    public int DistributeFileToDeploymentFolderWait { get; set; }
    public string HomeDriveLocation { get; set; }
    public string ManagerApiEndpointGetRequestedDownloadFileInformation { get; set; }
    public string ManagerApiEndpointUpdateDownloadProgressDistributed { get; set; }
    public string ManagerApiEndpointUpdateDownloadProgressDeleted { get; set; }

    public string ToString()
    {
        return $"BuildType: {BuildType}, " +
            $"ManagerApiAddressGrpc: {ManagerApiAddressGrpc}, " +
            $"ManagerApiAddressRestConfigBaseUrl: {ManagerApiAddressRestConfigBaseUrl.ToString()}, " +
            $"ManagerApiAddressRestConfigTimeout: {ManagerApiAddressRestConfigTimeout.ToString()}, " +
            $"CallManagerApiViaRest: {CallManagerApiViaRest}, " +
            $"DownloaderShareFilePath: {DownloaderShareFilePath}, " +
            $"PartFileExt: {PartFileExt}, " +
            $"TempMp4FileExt: {TempMp4FileExt}, " +
            $"M4aFileExt: {M4aFileExt}, " +
            $"YtdlpFileExt: {YtdlpFileExt}, " +
            $"FragPartFileContains: {FragPartFileContains}, " +
            //$"FormatTypeWhitelist: {FormatTypeWhitelist.ToString()}";
            $"DistributeFileToDeploymentFolderMaxRetry: {DistributeFileToDeploymentFolderMaxRetry}, " +
            $"DistributeFileToDeploymentFolderWait: {DistributeFileToDeploymentFolderWait}, " +
            $"HomeDriveLocation: {HomeDriveLocation}, " +
            $"ManagerApiEndpointGetRequestedDownloadFileInformation: {ManagerApiEndpointGetRequestedDownloadFileInformation}, " +
            $"ManagerApiEndpointUpdateDownloadProgressDistributed: {ManagerApiEndpointUpdateDownloadProgressDistributed}, " +
            $"ManagerApiEndpointUpdateDownloadProgressDeleted: {ManagerApiEndpointUpdateDownloadProgressDeleted}";
    }
}