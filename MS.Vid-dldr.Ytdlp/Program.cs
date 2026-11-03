

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using MS.Vid_dldr.Ytdlp;

Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[INITIALISE] MS.Vid-dldr.Ytdlp [v{GetVersion()}] started!");

_ = new Mutex(true, "MS.Vid-dldr.Ytdlp", out var instanceDoesntExists);
if (!instanceDoesntExists)
{
    Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[ERROR] Instance of MS.Vid-dldr.Ytdlp already exist");
    Console.ReadLine();
    System.Environment.Exit(1);
}

var appsettingsBuild = "";
#if !DEBUG
appsettingsBuild = ".Release";
#endif

Host.CreateDefaultBuilder()
    .UseContentRoot(Directory.GetCurrentDirectory())
    .ConfigureAppConfiguration((hostingContext, config) =>
        {
            config
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings{appsettingsBuild}.json", optional: true, reloadOnChange: true);
        })
        .ConfigureServices((ContextBoundObject, services) =>
        {
            services.AddTransient<IAppSettings, AppSettings>();
            services.AddSingleton<IManagerApiConfig, ManagerApiConfig>();

            services.AddHttpClient("ms-vid-dldr-httpClient", (serviceProvider, httpClientOptions) =>
                {
                    var clientConfig = serviceProvider.GetRequiredService<IManagerApiConfig>();

                    httpClientOptions.BaseAddress = clientConfig.BaseUrl;
                    httpClientOptions.Timeout = clientConfig.Timeout;
                })
                .AddResilienceHandler("ytdlp-pipeline", static builder =>
                {
                    builder.AddRetry(new HttpRetryStrategyOptions
                    {
                        BackoffType = DelayBackoffType.Exponential,
                        MaxRetryAttempts = 5,
                        UseJitter = true,
                        OnRetry = static args =>
                        {
                            Console.WriteLine($"{Constantss.MsNoticeLogPrefix}[WARN] REST Retry {args.AttemptNumber} after {args.RetryDelay.TotalMilliseconds}ms, due to: {args.Outcome.Result?.StatusCode.ToString() ?? args.Outcome.Exception?.GetType().Name}");
                            return default;
                        },
                    });

                    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        SamplingDuration = TimeSpan.FromSeconds(5),
                        FailureRatio = 0.9,
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromSeconds(5),
                        ShouldHandle = static args =>
                            {
                                return ValueTask.FromResult(args is
                                {
                                    Outcome.Result.StatusCode:
                                HttpStatusCode.RequestTimeout or
                                    HttpStatusCode.TooManyRequests
                                });
                            }
                    });

                    builder.AddTimeout(TimeSpan.FromSeconds(15));
                });

            services.AddHostedService<YtdlpService>();
        })
        .Build()
        .Run();

static string GetVersion()
{
    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
    System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);

    return fvi.FileVersion;
}