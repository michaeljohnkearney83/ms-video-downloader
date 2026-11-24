using MS.Vid_dldr.DownloadUpdater.Observability;
using System.Diagnostics;
using System.Text;
using MS.Vid_dldr.DownloadUpdater;

namespace MS.Vid_dldr.DownloadUpdater.Tests;

public class TelemetryTests : IDisposable
{
    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = source => source.Name == Telemetry.ServiceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    public TelemetryTests() => ActivitySource.AddActivityListener(_listener);

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void StartConsumeActivity_ContinuesTheTraceInTheTraceparentHeader()
    {
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        const string parentSpanId = "b7ad6b7169203331";
        // RabbitMQ.Client delivers string header values as byte[].
        var headers = new Dictionary<string, object> { ["traceparent"] = Encoding.UTF8.GetBytes($"00-{traceId}-{parentSpanId}-01") };

        using var activity = Telemetry.StartConsumeActivity(headers, "ms_vd_q_downloadUpdate");

        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Consumer, activity!.Kind);
        Assert.Equal(traceId, activity.TraceId.ToString());
        Assert.Equal(parentSpanId, activity.ParentSpanId.ToString());
        Assert.Equal("ms_vd_q_downloadUpdate", activity.GetTagItem("messaging.destination.name"));
    }

    [Fact]
    public void StartConsumeActivity_WithoutHeaders_StartsANewTrace()
    {
        using var activity = Telemetry.StartConsumeActivity(null, "q");

        Assert.NotNull(activity);
        Assert.Equal(default, activity!.ParentSpanId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    public void ReadHeader_MissingHeader_ReturnsNothing(string? key)
    {
        var headers = key == null ? null : new Dictionary<string, object> { ["other"] = "x" };

        Assert.Empty(Telemetry.ReadHeader(headers, "traceparent"));
    }
}
