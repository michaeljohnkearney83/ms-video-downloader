using Microsoft.Extensions.Configuration;

namespace MS.Vid_dldr.Ytdlp;

public interface IManagerApiConfig
{
    Uri BaseUrl { get; set; }
    TimeSpan Timeout { get; set; }

    string ToString();
}