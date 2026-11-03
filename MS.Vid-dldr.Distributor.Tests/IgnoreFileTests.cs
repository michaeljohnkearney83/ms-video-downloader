using System.Net;

namespace MS.Vid_dldr.Distributor.Tests;

public class IgnoreFileTests
{
    private const string Id = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    private static DistributorService CreateService(TestAppSettings? settings = null) =>
        new(settings ?? new TestAppSettings(),
            new FakeHttpClientFactory(new FakeManagerHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

    [Theory]
    [InlineData(Id + ".mp4")]
    [InlineData(Id + ".webm")]
    [InlineData(Id + ".mkv")]
    public void Finished_download_named_by_guid_is_not_ignored(string name)
    {
        Assert.False(CreateService().IgnoreFile(name));
    }

    [Theory]
    [InlineData("not-a-guid.mp4")]                       // client id must be a GUID
    [InlineData(Id)]                                     // no extension
    [InlineData(Id + ".f140.m4a")]                       // more than one '.'
    [InlineData(Id + ".part")]                           // still downloading
    [InlineData(Id + ".m4a")]                            // audio-only intermediate
    [InlineData(Id + ".ytdlp")]
    [InlineData(Id + ".json")]                           // info file
    [InlineData(Id + ".f1234567890123456v")]             // BBC fragment: 18 chars, f...v
    [InlineData(Id + ".fmf_cloudfront-video=123")]       // Facebook fragment
    public void Intermediate_or_unrelated_file_is_ignored(string name)
    {
        Assert.True(CreateService().IgnoreFile(name));
    }

    [Fact]
    public void File_ending_with_a_listed_format_is_ignored()
    {
        // Entries such as "f140.m4a" only match names that already have two dots (ignored anyway),
        // so use an entry without a dot to exercise the list check itself.
        var settings = new TestAppSettings { FormatTypeWhitelist = new() { "webm" } };

        Assert.True(CreateService(settings).IgnoreFile(Id + ".webm"));
        Assert.False(CreateService(settings).IgnoreFile(Id + ".mp4"));
    }

    [Fact]
    public void Missing_format_list_does_not_throw()
    {
        var settings = new TestAppSettings { FormatTypeWhitelist = null! };

        Assert.False(CreateService(settings).IgnoreFile(Id + ".mp4"));
    }

    [Theory]
    [InlineData(Id + ".f1234567890123456v", true)]
    [InlineData(Id + ".f123456789012345v", false)]       // 17 chars
    [InlineData(Id + ".x1234567890123456v", false)]
    public void IsBbcIgnore_matches_18_char_f_v_extension(string name, bool expected)
    {
        Assert.Equal(expected, CreateService().IsBbcIgnore(name));
    }

    [Theory]
    [InlineData(Id + ".fmf_cloudfront-video=abc", true)]
    [InlineData(Id + ".mp4", false)]
    public void IsFacebookIgnore_matches_cloudfront_extension(string name, bool expected)
    {
        Assert.Equal(expected, CreateService().IsFacebookIgnore(name));
    }
}
