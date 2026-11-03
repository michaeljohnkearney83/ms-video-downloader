using System.Text.Json;
using MS.Vid_dldr.Manager.Helpers;

namespace MS.Vid_dldr.Manager.Tests;

public class UtilityHelperTests
{
    [Fact]
    public void GetDateTimeNowUtc_ReturnsUtcNow()
    {
        var before = DateTime.UtcNow;
        var now = UtilityHelper.GetDateTimeNowUtc();
        var after = DateTime.UtcNow;

        Assert.Equal(DateTimeKind.Utc, now.Kind);
        Assert.InRange(now, before, after);
    }

    [Fact]
    public void ToJsonString_Null_ReturnsEmpty()
    {
        Assert.Equal("", UtilityHelper.ToJsonString(null));
    }

    [Fact]
    public void ToJsonString_Document_ReturnsIndentedEquivalentJson()
    {
        using var doc = JsonDocument.Parse("{\"a\":1,\"b\":[\"x\"]}");

        var json = UtilityHelper.ToJsonString(doc);

        Assert.Contains(Environment.NewLine, json);
        using var roundTrip = JsonDocument.Parse(json);
        Assert.Equal(1, roundTrip.RootElement.GetProperty("a").GetInt32());
        Assert.Equal("x", roundTrip.RootElement.GetProperty("b")[0].GetString());
    }

    [Fact]
    public void GetProtoTimestampNotSet_IsUnixEpoch()
    {
        var ts = UtilityHelper.GetProtoTimestampNotSet();

        Assert.Equal(0, ts.Seconds);
        Assert.Equal(0, ts.Nanos);
    }

    [Fact]
    public void RandomInt_IsBetweenOneAndNine()
    {
        for (var i = 0; i < 100; i++)
            Assert.InRange(UtilityHelper.RandomInt(), 1, 9);
    }

    [Fact]
    public void SetKind_ChangesKindOnly()
    {
        var local = new DateTime(2024, 6, 5, 10, 30, 0, DateTimeKind.Local);

        var utc = local.SetKind(DateTimeKind.Utc);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(local.Ticks, utc.Ticks);
    }

    [Fact]
    public void GetMinWithKind_ReturnsMinValueWithKind()
    {
        var min = UtilityHelper.GetMinWithKind(DateTimeKind.Utc);

        Assert.Equal(DateTime.MinValue.Ticks, min.Ticks);
        Assert.Equal(DateTimeKind.Utc, min.Kind);
    }
}
