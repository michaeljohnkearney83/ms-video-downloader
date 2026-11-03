using MS.Vid_dldr.DownloaderUpdater.Mappers;
using MS.Vid_dldr.DownloadUpdater.Dtos.Extract;

namespace MS.Vid_dldr.DownloadUpdater.Tests;

public class VdMapperTests
{
    [Fact]
    public void Null_dto_gives_empty_extract_info()
    {
        Assert.Empty(ConfigureVdMapper.Map(null!).Formats);
    }

    [Fact]
    public void Null_formats_gives_empty_extract_info()
    {
        Assert.Empty(ConfigureVdMapper.Map(new ExtractInfoDto { Formats = null! }).Formats);
    }

    [Fact]
    public void Empty_format_fields_map_to_proto_defaults()
    {
        var dto = new ExtractInfoDto { Formats = new() { new FormatDto() } };

        var format = Assert.Single(ConfigureVdMapper.Map(dto).Formats);

        Assert.Equal("", format.FormatId);
        Assert.Equal("", format.Url);
        Assert.Equal(0, format.Width);
        Assert.Equal(0.0, format.Fps);
        Assert.Equal(0, format.HasDrm);
        Assert.Empty(format.Fragments);
        Assert.Equal(0, format.DownloaderOptions.HttpChunkSize);
    }

    [Fact]
    public void Format_values_fragments_and_options_are_mapped()
    {
        var dto = new ExtractInfoDto
        {
            Formats = new()
            {
                new FormatDto
                {
                    FormatId = "137",
                    Ext = "mp4",
                    Vcodec = "avc1.640028",
                    Url = "https://example.test/v.mp4",
                    Width = 1920,
                    Height = 1080,
                    Fps = 29.97,
                    Tbr = 4400.5,
                    Filesize = 123456,
                    HasDrm = true,
                    Fragments = new() { new FragmentDto { Url = "frag1", Duration = 5.5 }, new FragmentDto { Url = "frag2" } },
                    DownloaderOptions = new DownloaderOptionsDto { HttpChunkSize = 10485760 },
                },
            },
        };

        var format = Assert.Single(ConfigureVdMapper.Map(dto).Formats);

        Assert.Equal("137", format.FormatId);
        Assert.Equal("mp4", format.Ext);
        Assert.Equal("avc1.640028", format.Vcodec);
        Assert.Equal(1920, format.Width);
        Assert.Equal(1080, format.Height);
        Assert.Equal(29.97, format.Fps);
        Assert.Equal(4400.5, format.Tbr);
        Assert.Equal(123456, format.Filesize);
        Assert.Equal(1, format.HasDrm);
        Assert.Equal(10485760, format.DownloaderOptions.HttpChunkSize);
        Assert.Collection(format.Fragments,
            f => { Assert.Equal("frag1", f.Url); Assert.Equal(5.5, f.Duration); },
            f => { Assert.Equal("frag2", f.Url); Assert.Equal(0.0, f.Duration); });
    }
}
