using System.Text.Json.Nodes;
using MS.Vid_dldr.Manager.Dtos;
using MS.Vid_dldr.Manager.Helpers;
using MS.Vid_dldr.Manager.Messaging;

namespace MS.Vid_dldr.Manager.Tests;

public class DownloadActionsTests
{
    [Theory]
    [InlineData("REQUESTED")]
    [InlineData("QUEUED")]
    [InlineData("ANALYSIS")]
    [InlineData("DOWNLOADING")]
    [InlineData("RETRY")]
    public void QueuedOrRunning_CanBePausedAndCancelled_NotResumed(string status)
    {
        Assert.True(DownloadActions.CanPause(status));
        Assert.True(DownloadActions.CanCancel(status));
        Assert.False(DownloadActions.CanResume(status));
    }

    [Fact]
    public void Paused_CanBeResumedOrCancelled_NotPausedAgain()
    {
        Assert.True(DownloadActions.CanResume(Constants.DownloadStatusTypePaused));
        Assert.True(DownloadActions.CanCancel(Constants.DownloadStatusTypePaused));
        Assert.False(DownloadActions.CanPause(Constants.DownloadStatusTypePaused));
    }

    [Theory]
    [InlineData("FINISHED")]
    [InlineData("DISTRIBUTED")]
    [InlineData("ERRORED")]
    [InlineData("DELETED")]
    [InlineData("CANCELLED")]
    public void FinishedOrGone_AllowsNothing(string status)
    {
        Assert.NotNull(DownloadActions.Validate(DownloadActions.Pause, status));
        Assert.NotNull(DownloadActions.Validate(DownloadActions.Resume, status));
        Assert.NotNull(DownloadActions.Validate(DownloadActions.Cancel, status));
    }

    [Fact]
    public void Validate_ExplainsWhyNot()
    {
        Assert.Equal("Only a paused download can be resumed; this one is DOWNLOADING",
            DownloadActions.Validate(DownloadActions.Resume, "DOWNLOADING"));
        Assert.Null(DownloadActions.Validate(DownloadActions.Pause, "DOWNLOADING"));
        Assert.Equal("Unknown action STOP", DownloadActions.Validate("STOP", "DOWNLOADING"));
    }

    [Theory]
    [InlineData("PAUSED", true)]
    [InlineData("CANCELLED", true)]
    [InlineData("DOWNLOADING", false)]
    [InlineData("ERRORED", false)]
    public void ProgressIsIgnored_OnlyForDownloadsTheUserStopped(string status, bool ignored)
    {
        Assert.Equal(ignored, DownloadActions.IsStoppedByUser(status));
    }

    [Fact]
    public void ControlMessage_HasTheFieldsTheDownloaderReads()
    {
        var clientId = Guid.Parse("a1b2c3d4-e5f6-4789-abcd-ef0123456789");

        var json = JsonNode.Parse(new DownloadControlDto { Action = DownloadActions.Pause, Id = 15, ClientId = clientId }.ToString())!;

        Assert.Equal("CONTROL", json["messageType"]!.GetValue<string>());
        Assert.Equal("PAUSE", json["action"]!.GetValue<string>());
        Assert.Equal(15, json["managerId"]!.GetValue<int>());
        Assert.Equal(clientId.ToString(), json["downloadId"]!.GetValue<string>());
    }

    [Fact]
    public void ResumedRequest_CarriesTheResumeFlag()
    {
        var json = JsonNode.Parse(new DownloadRequestDto { Id = 1, Resume = true }.ToString())!;

        Assert.True(json["resume"]!.GetValue<bool>());
        Assert.False(JsonNode.Parse(new DownloadRequestDto { Id = 1 }.ToString())!["resume"]!.GetValue<bool>());
    }

    [Fact]
    public void ControlExchange_IsANonDurableFanout()
    {
        Assert.Equal("exchange:ms_vd_x_downloadControl?type=fanout&durable=false",
            MessagingSetup.GetControlExchangeAddress("ms_vd_x_downloadControl").ToString());
    }
}
