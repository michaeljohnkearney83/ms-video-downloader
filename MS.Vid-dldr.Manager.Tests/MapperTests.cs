using AutoMapper;
using MS.Vid_dldr.DownloadUpdater.Dtos.Extract; // FragmentDto lives in this namespace inside the Manager project
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Dtos.Extract;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Mappers;
using MS.Vid_dldr.Manager.Profiles;
using MS.Vid_dldr.Manager.Protos.FromClient;

namespace MS.Vid_dldr.Manager.Tests;

public class MapperTests
{
    private static readonly Guid SessionId = Guid.Parse("3f2b8c1e-6a4d-4f0e-9b7a-2c5d8e1f0a34");
    private static readonly Guid ClientId = Guid.Parse("a1b2c3d4-e5f6-4789-abcd-ef0123456789");

    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<DownloadProfiles>();
            cfg.AddProfile<ExtractInfoProfile>();
        }).CreateMapper();

    private static Download CreateDownload() => new()
    {
        Id = 42,
        ClientId = ClientId,
        RequestedOn = new DateTime(2025, 1, 13, 9, 59, 40, DateTimeKind.Utc),
        RequestedUrl = "https://example.com/watch?v=abc",
        RequestedDownloadFolder = "E:\\123\\",
        RequestedDownloadName = "video",
        RequestedDownloadExt = "mp4",
        RequestedRetryCount = 3,
        RequestedTimeoutSeconds = 600,
        RequestedDownloadStalledTimeoutSeconds = 60,
        LastActivity = new DateTime(2025, 1, 13, 10, 0, 0, DateTimeKind.Utc),
        Status = Constants.DownloadStatusTypeRequested,
        RetryOnErroredAttemptNo = 0,
        RetryOnErroredMaxCount = 3,
        DownloadSessionId = SessionId,
        DownloadSession = new DownloadSession
        {
            Id = SessionId,
            Username = "user",
            Fid = "F1",
            ClientName = "client",
            DeploymentNo = "123",
            RequestedOn = new DateTime(2025, 1, 13, 9, 0, 0, DateTimeKind.Utc),
            LastActivity = new DateTime(2025, 1, 13, 10, 0, 0, DateTimeKind.Utc),
        },
    };

    [Fact]
    public void RequestDownloadRequest_To_Download_MapsRequestFields()
    {
        var request = new RequestDownloadRequest
        {
            DownloadSessionId = SessionId.ToString(),
            RequestedUrl = "https://example.com/watch?v=abc",
            RequestedDownloadFolder = "",
            RequestedDownloadName = "video",
            RequestedDownloadExt = "mkv",
            RequestedRetryCount = 2,
            RequestedTimeoutSeconds = 300,
            RequestedDownloadStalledTimeoutSeconds = 30,
        };

        var download = CreateMapper().Map<Download>(request);

        Assert.Equal(SessionId, download.DownloadSessionId);
        Assert.Equal("https://example.com/watch?v=abc", download.RequestedUrl);
        Assert.Equal("video", download.RequestedDownloadName);
        Assert.Equal("mkv", download.RequestedDownloadExt);
        Assert.Equal(2, download.RequestedRetryCount);
        Assert.Equal(300, download.RequestedTimeoutSeconds);
        Assert.Equal(30, download.RequestedDownloadStalledTimeoutSeconds);
    }

    [Fact]
    public void Download_To_DownloadRequestDto_MapsAndStringifiesNumbers()
    {
        var dto = CreateMapper().Map<DownloadRequestDto>(CreateDownload());

        Assert.Equal("DOWNLOAD", dto.MessageType);
        Assert.Equal(42, dto.Id);
        Assert.Equal(ClientId, dto.ClientId);
        Assert.Equal("https://example.com/watch?v=abc", dto.RequestedUrl);
        Assert.Equal("video", dto.RequestedDownloadName);
        Assert.Equal("mp4", dto.RequestedDownloadExt);
        Assert.Equal("3", dto.RequestedRetryCount);
        Assert.Equal("600", dto.RequestedTimeoutSeconds);
        Assert.Equal("60", dto.RequestedDownloadStalledTimeoutSeconds);
        Assert.Contains("\"downloadId\":\"" + ClientId + "\"", dto.ToString());
    }

    [Fact]
    public void Download_To_AnalysisRequestDto_Maps()
    {
        var dto = CreateMapper().Map<AnalysisRequestDto>(CreateDownload());

        Assert.Equal("ANALYSIS", dto.MessageType);
        Assert.Equal(42, dto.Id);
        Assert.Equal(ClientId, dto.ClientId);
        Assert.Equal("3", dto.RequestedRetryCount);
        Assert.False(dto.IsRetry);
    }

    [Fact]
    public void RequestExtractInfoFormatsRequest_To_ExtractInfo_Maps()
    {
        var request = new RequestExtractInfoFormatsRequest
        {
            DownloadSessionId = SessionId.ToString(),
            RequestedUrl = "https://example.com/watch?v=abc",
            TimeoutMs = 3000,
        };

        var extractInfo = CreateMapper().Map<Entities.ExtractInfo>(request);

        Assert.Equal(SessionId, extractInfo.DownloadSessionId);
        Assert.Equal("https://example.com/watch?v=abc", extractInfo.RequestedUrl);
    }

    [Fact]
    public void ConfigureVdMapper_Map_NullList_ReturnsEmpty()
    {
        Assert.Empty(ConfigureVdMapper.Map((List<Download>)null!));
    }

    [Fact]
    public void ConfigureVdMapper_Map_MapsDownloadAndDefaultsEmptyFields()
    {
        var result = Assert.Single(ConfigureVdMapper.Map(new List<Download> { CreateDownload() }));

        Assert.Equal(42, result.Id);
        Assert.Equal(SessionId.ToString(), result.SessionId);
        Assert.Equal("F1", result.ForceId);
        Assert.Equal("user", result.Username);
        Assert.Equal("client", result.Client);
        Assert.Equal("123", result.DeploymentNo);
        Assert.Equal(ClientId.ToString(), result.Requested.ClientId);
        Assert.Equal("https://example.com/watch?v=abc", result.Requested.Url);
        Assert.Equal(3, result.Requested.RetryCount);
        Assert.Equal(Constants.DownloadStatusTypeRequested, result.Status);
        Assert.Equal("", result.VideoAnalysis.Title);
        Assert.Equal("", result.Filename);
        Assert.Equal("", result.ErrorMessage);
        Assert.Equal(new DateTime(2025, 1, 13, 10, 0, 0, DateTimeKind.Utc), result.LastActivity.ToDateTime());
    }

    [Fact]
    public void ClientVdMapper_Map_Null_ReturnsEmptyExtractInfo()
    {
        Assert.Empty(ClientVdMapper.Map(null!).Formats);
    }

    [Fact]
    public void ClientVdMapper_Map_NullFormats_ReturnsEmptyExtractInfo()
    {
        Assert.Empty(ClientVdMapper.Map(new ExtractInfoDto { Formats = null! }).Formats);
    }

    [Fact]
    public void ClientVdMapper_Map_MapsFormatsAndFragments()
    {
        var dto = new ExtractInfoDto
        {
            Formats = new List<FormatDto>
            {
                new()
                {
                    FormatId = "137", FormatNote = "1080p", Ext = "mp4", Protocol = "https",
                    Acodec = "none", Vcodec = "avc1", Url = "https://cdn.example.com/137",
                    AudioExt = "none", VideoExt = "mp4", Resolution = "1920x1080",
                    FormatTitle = "137 - 1920x1080", ManifestUrl = "", Language = "", DynamicRange = "SDR",
                    Container = "mp4_dash",
                    Fragments = new List<FragmentDto> { new() { Url = "https://cdn.example.com/f1" } },
                },
            },
        };

        var proto = ClientVdMapper.Map(dto);

        var format = Assert.Single(proto.Formats);
        Assert.Equal("137", format.FormatId);
        Assert.Equal("1080p", format.FormatNote);
        Assert.Equal("1920x1080", format.Resolution);
        Assert.Equal("https://cdn.example.com/f1", Assert.Single(format.Fragments).Url);
        Assert.NotNull(format.DownloaderOptions);
    }
}
