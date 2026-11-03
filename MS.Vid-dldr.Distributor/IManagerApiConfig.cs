using Microsoft.Extensions.Configuration;

namespace MS.Vid_dldr.Distributor;

public interface IManagerApiConfig
{
    Uri BaseUrl { get; set; }
    TimeSpan Timeout { get; set; }
}