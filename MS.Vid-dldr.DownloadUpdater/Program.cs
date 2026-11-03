using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MS.Vid_dldr.DownloadUpdater;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client.Balancer;

Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[INITIALISE] MS.Vid-dldr.DownloadUpdater started");

AppSettings _appSettings = null;

Host.CreateDefaultBuilder()
    .UseContentRoot(Directory.GetCurrentDirectory())
    .ConfigureAppConfiguration((hostingContext, cfg) =>
    {
        var configurationBuilder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile($"appsettings.json", false, true);

#if !DEBUG
        configurationBuilder.AddJsonFile($"appsettings.Release.json", true, true);
#endif

        IConfiguration configuration = configurationBuilder.Build();
        _appSettings = new(configuration);
    })
    .ConfigureLogging(logging =>
    {
        logging.AddFilter("Grpc", LogLevel.Debug);
    })
    .ConfigureServices((ContextBoundObject, services) =>
    {
        services.AddTransient<IAppSettings, AppSettings>();
        services.AddHostedService<UpdaterService>();
        services.AddSingleton<ResolverFactory>(
            x => new DnsResolverFactory(refreshInterval: TimeSpan.FromSeconds(30)));
    })
    .Build()
    .Run();