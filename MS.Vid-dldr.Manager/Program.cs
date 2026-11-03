using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MS.Vid_dldr.Manager.Services;
using MS.Vid_dldr.Manager.Services.Internal;
using MS.Vid_dldr.Manager.Data;
using MS.Vid_dldr.Manager;
using MS.Vid_dldr.Manager.Helpers;
using Microsoft.OpenApi.Models;
using Serilog;

Console.WriteLine($"{Constants.MsNoticeLogPrefix}[INITIALISE] MS.Vid-dldr.Manager [v{UtilityHelper.GetVersion()}] started!");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<IAppSettings, AppSettings>();
builder.Services.AddTransient<IServiceHelper, ServiceHelper>();
builder.Services.AddTransient<IVersionService, VersionService>();
builder.Services.AddTransient<AppSettings>();
builder.Services.AddTransient<VersionService>();
builder.Services.AddGrpcReflection();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DownloadsDb"), npgsql => npgsql.EnableRetryOnFailure(
        maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null));
    options.EnableSensitiveDataLogging();
});

builder.Services.AddGrpc().AddJsonTranscoding();
builder.Services.AddCors(o => o.AddPolicy("AllowAll", builder =>
{
    builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
}));
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpcSwagger();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x =>
{
    x.SwaggerDoc("GenericService", new OpenApiInfo { Title = "GenericService", Version = "v1" });
    x.SwaggerDoc("ClientService", new OpenApiInfo { Title = "ClientService", Version = "v1" });
    x.SwaggerDoc("ConfigureService", new OpenApiInfo { Title = "ConfigureService", Version = "v1" });
    x.SwaggerDoc("UpdaterService", new OpenApiInfo { Title = "UpdaterService", Version = "v1" });
    x.SwaggerDoc("DistributorService", new OpenApiInfo { Title = "DistributorService", Version = "v1" });
    x.SwaggerDoc("YtdlpService", new OpenApiInfo { Title = "YtdlpService", Version = "v1" });

    x.CustomSchemaIds(t => t.FullName.Replace(".", "_"));

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    x.IncludeXmlComments(xmlPath);
    x.IncludeGrpcXmlComments(xmlPath, includeControllerXmlComments: true);
});

var _logger = new LoggerConfiguration()
.ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext()
.CreateLogger();

builder.Logging.AddSerilog(_logger);

var app = builder.Build();

using var scopeDbContext = app.Services.CreateScope();
await using var dbContext = scopeDbContext.ServiceProvider.GetRequiredService<AppDbContext>();
// The API often starts before Postgres is ready (docker compose), so retry the migration.
var startupDbRetryCount = builder.Configuration.GetValue("AppSettings:StartupDbRetryCount", 10);
await StartupRetry.RunAsync(
    ct => dbContext.Database.MigrateAsync(ct),
    startupDbRetryCount,
    TimeSpan.FromSeconds(5),
    (attempt, ex) => Console.WriteLine($"{Constants.MsNoticeLogPrefix}[WARN] Database migration attempt {attempt}/{startupDbRetryCount} failed: {ex.Message}. Retrying in 5s..."));

using var scopeAppSettings = app.Services.CreateScope();
var appSettings = scopeAppSettings.ServiceProvider.GetRequiredService<AppSettings>();
Console.WriteLine($"{Constants.MsNoticeLogPrefix}[APPSETTINGS]\r\n{appSettings.ToString()}");

using var scopeVersionService = app.Services.CreateScope();
var versionService = scopeVersionService.ServiceProvider.GetRequiredService<VersionService>();
versionService.InitiateCollectionOfVersions();

IWebHostEnvironment env = app.Environment;
if (env.IsDevelopment())
{
    Console.WriteLine($"{Constants.MsNoticeLogPrefix} Swagger available");

    app.MapGrpcReflectionService();
    Log.Information("gRPC Reflection enabled");

    app.UseSwagger();
    app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/GenericService/swagger.json", "GenericService");
            c.SwaggerEndpoint("/swagger/ClientService/swagger.json", "ClientService");
            c.SwaggerEndpoint("/swagger/ConfigureService/swagger.json", "ConfigureService");
            c.SwaggerEndpoint("/swagger/UpdaterService/swagger.json", "UpdaterService");
            c.SwaggerEndpoint("/swagger/DistributorService/swagger.json", "DistributorService");
            c.SwaggerEndpoint("/swagger/YtdlpService/swagger.json", "YtdlpService");
        });
}

app.UseCors();

app.MapGrpcService<VdApiCallsForGenericService>().RequireCors("AllowAll");
app.MapGrpcService<VdApiCallsForClientService>().RequireCors("AllowAll");
app.MapGrpcService<VdApiCallsForConfigureService>().RequireCors("AllowAll");
app.MapGrpcService<VdApiCallsForUpdaterService>().RequireCors("AllowAll");
app.MapGrpcService<VdApiCallsForDistributorService>().RequireCors("AllowAll");
app.MapGrpcService<VdApiCallsForYtdlpService>().RequireCors("AllowAll");
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

Console.WriteLine($"{Constants.MsNoticeLogPrefix} RUN!");
app.Run();