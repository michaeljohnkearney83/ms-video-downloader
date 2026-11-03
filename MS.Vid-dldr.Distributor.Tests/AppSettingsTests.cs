using Microsoft.Extensions.Configuration;

namespace MS.Vid_dldr.Distributor.Tests;

public class AppSettingsTests
{
    private static IConfiguration Config(Dictionary<string, string?> extra)
    {
        var values = new Dictionary<string, string?>
        {
            ["ManagerApiRestConfig:BaseUrl"] = "http://localhost:8000",
            ["ManagerApiRestConfig:Timeout"] = "00:00:30",
            ["CallManagerApiViaRest"] = "true",
            ["DistributeFileToDeploymentFolderMaxRetry"] = "3",
            ["DistributeFileToDeploymentFolderWait"] = "3000",
        };
        foreach (var (key, value) in extra) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Format_list_keeps_only_entries_set_to_true()
    {
        var settings = new AppSettings(Config(new()
        {
            ["FormatTypeWhitelist:f140.m4a"] = "true",
            ["FormatTypeWhitelist:f251.webm"] = "false",
        }));

        Assert.Equal(new[] { "f140.m4a" }, settings.FormatTypeWhitelist);
        Assert.True(settings.CallManagerApiViaRest);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.ManagerApiAddressRestConfigTimeout);
    }

    [Fact]
    public void Missing_format_list_gives_empty_list()
    {
        var settings = new AppSettings(Config(new()));

        Assert.Empty(settings.FormatTypeWhitelist);
    }
}
