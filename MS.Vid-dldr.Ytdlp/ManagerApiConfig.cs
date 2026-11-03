using Microsoft.Extensions.Configuration;

namespace MS.Vid_dldr.Ytdlp;

public class ManagerApiConfig : IManagerApiConfig
{
    public ManagerApiConfig(IConfiguration configuration)
    {
        BaseUrl = new Uri(configuration["ManagerApiRestConfig:BaseUrl"]);
        Timeout = TimeSpan.Parse(configuration["ManagerApiRestConfig:Timeout"]);
    }

    public Uri BaseUrl { get; set; }
    public TimeSpan Timeout { get; set; }

    public override string ToString()
    {
        return $"BaseUrl: {BaseUrl}, " +
            $"Timeout: {Timeout.ToString()}";
    }
}