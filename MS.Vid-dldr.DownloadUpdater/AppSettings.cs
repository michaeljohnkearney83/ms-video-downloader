using Microsoft.Extensions.Configuration;

namespace MS.Vid_dldr.DownloadUpdater;

public class AppSettings : IAppSettings
{
    public AppSettings(IConfiguration configuration)
    {
        ManagerApiAddress = configuration["AppSettings:ManagerApiAddress"];
        ChannelHostName = configuration["AppSettings:ChannelHostName"];
        ChannelPortNumber = Convert.ToInt32(configuration["AppSettings:ChannelPortNumber"]);
        QueueRequestDownload = configuration["AppSettings:QueueRequestDownload"];
        QueueDownloadUpdate = configuration["AppSettings:QueueDownloadUpdate"];
    }

    public string ManagerApiAddress { get; set; }
    public string ChannelHostName { get; set; }
    public int ChannelPortNumber { get; set; }
    public string QueueRequestDownload { get; set; }
    public string QueueDownloadUpdate { get; set; }
}
