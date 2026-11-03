import datetime as dt
import json
import types

import pytest
import yt_dlp

import appsettings
import constants as c
import main


# --- getValue / getProgressValue -------------------------------------------------------------------------------

@pytest.mark.parametrize("value", [None, "", "None", "N/A"])
def test_getValue_treats_empty_markers_as_empty_string(value):
    assert main.getValue({"k": value}, "k") == ""


def test_getValue_missing_key_is_empty_string():
    assert main.getValue({}, "k") == ""


@pytest.mark.parametrize("value", ["abc", 0, 12.5, ["f1"]])
def test_getValue_returns_real_values(value):
    assert main.getValue({"k": value}, "k") == value


def test_getProgressValue_strips_ansi_colours_and_whitespace():
    assert main.getProgressValue({"_percent_str": "\x1b[0;94m 42.0%\x1b[0m "}, "_percent_str") == "42.0%"


@pytest.mark.parametrize("msg", [{}, {"k": None}, {"k": "N/A"}])
def test_getProgressValue_missing_is_none(msg):
    assert main.getProgressValue(msg, "k") is None


# --- setGlobals / getYtdlpOptions ------------------------------------------------------------------------------

def test_setGlobals_reads_request_values(request_globals):
    assert main._managerId == 42
    assert main._url == "https://example.test/watch?v=abc"
    assert (main._retryCount, main._timeoutSeconds, main._downloadStalledTimeoutSeconds) == (5, 20, 60)


@pytest.mark.parametrize("missing", [None, ""])
def test_setGlobals_uses_int_defaults_when_values_missing_or_empty(missing):
    # The Manager sends these as (possibly null or empty) strings.
    main.setGlobals({"managerId": 1, "retryCount": missing, "timeoutSeconds": missing,
                     "downloadStalledTimeoutSeconds": missing})

    assert (main._retryCount, main._timeoutSeconds, main._downloadStalledTimeoutSeconds) == (3, 30, 138)
    assert main._downloadlExt == "mp4"


def test_getYtdlpOptions_uses_request_values(request_globals):
    opts = main.getYtdlpOptions()

    assert opts["outtmpl"] == "h-drive/3f2504e0-4f89-11d3-9a0c-0305e82c3301.%(ext)s"
    assert opts["retries"] == 5
    assert opts["socket_timeout"] == 20
    assert opts["progress_hooks"] == [main.progressHooks]


# --- loadAppSettings ---------------------------------------------------------------------------------------------

def test_loadAppSettings_converts_env_strings_to_types(monkeypatch):
    # Values exactly as .env.prod / docker compose pass them.
    monkeypatch.setenv("IS_DEBUG_MODE", "False")
    monkeypatch.setenv("CHANNEL_PORT_NUMBER", "5672")
    monkeypatch.setenv("DEFAULT_RETRY_COUNT", "10")
    monkeypatch.setenv("DEFAULT_TIMEOUT_SECONDS", "30")
    monkeypatch.setenv("DEFAULT_DOWNLOAD_STALLED_TIMEOUT_SECONDS", "300")
    monkeypatch.setenv("QUEUE_DOWNLOAD_UPDATE", "ms_vd_q_downloadUpdate")

    main.loadAppSettings()

    assert appsettings.IsDebugMode is False
    assert appsettings.ChannelPortNumber == 5672
    assert appsettings.DefaultRetryCount == 10
    assert appsettings.DefaultTimeoutSeconds == 30
    assert appsettings.DefaultDownloadStalledTimeoutSeconds == 300
    assert appsettings.QueueDownloadUpdate == "ms_vd_q_downloadUpdate"


@pytest.mark.parametrize("text, expected", [("True", True), ("true", True), ("1", True), ("False", False), ("0", False), ("no", False)])
def test_loadAppSettings_parses_debug_flag(monkeypatch, text, expected):
    monkeypatch.setenv("IS_DEBUG_MODE", text)
    main.loadAppSettings()
    assert appsettings.IsDebugMode is expected


def test_loadAppSettings_keeps_defaults_without_env():
    main.loadAppSettings()
    assert (appsettings.DefaultRetryCount, appsettings.ChannelHost, appsettings.IsDebugMode) == (3, "rabbitmq", True)


def test_env_retry_default_reaches_ytdlp_as_int(monkeypatch):
    monkeypatch.setenv("DEFAULT_RETRY_COUNT", "10")
    main.loadAppSettings()
    main.setGlobals({"managerId": 1})

    assert main.getYtdlpOptions()["retries"] == 10   # a str here makes yt-dlp's RetryManager raise TypeError


# --- download stall detection -------------------------------------------------------------------------------------

def test_hasDownloadStalled(monkeypatch, request_globals):
    now = [dt.datetime(2026, 11, 3, 21, 0, 0)]
    monkeypatch.setattr(main.h, "getDateTimeNow", lambda: now[0])
    main.resetDownloadStalled()

    assert main.hasDownloadStalled("10.0%") is False          # progress moved
    now[0] += dt.timedelta(seconds=59)
    assert main.hasDownloadStalled("10.0%") is False          # same %, within 60 s
    now[0] += dt.timedelta(seconds=2)
    assert main.hasDownloadStalled("10.0%") is True           # same % for 61 s
    assert main.hasDownloadStalled("11.0%") is False          # moved again, timer restarts


# --- progressHooks ------------------------------------------------------------------------------------------------

def downloading(percent="42.0%", eta="00:10"):
    return {"status": "downloading", "_percent_str": percent, "_eta_str": eta, "_speed_str": "1.2MiB/s",
            "_total_bytes_str": "100MiB", "_downloaded_bytes_str": "42MiB", "_elapsed_str": "00:05",
            "_default_template": "[download] 42.0%", "filename": "h-drive/x.mp4"}


def test_progressHooks_sends_downloading_update(sent, request_globals):
    main.resetDownloadStalled()
    main.progressHooks(downloading())

    queue, message = sent[0]
    assert queue == "ms_vd_q_downloadUpdate"
    assert message["managerId"] == 42
    assert message["status"] == c.DownloadStatusTypeDownloading
    assert (message["percent"], message["eta"], message["speed"], message["filename"]) == ("42.0%", "00:10", "1.2MiB/s", "h-drive/x.mp4")
    dt.datetime.strptime(message["activity"], "%d/%m/%Y %H:%M:%S")


@pytest.mark.parametrize("msg", [downloading(eta="00:00"), downloading(percent="100.0%"), {"status": "finished"}])
def test_progressHooks_sends_finished(sent, request_globals, msg):
    main.resetDownloadStalled()
    main.progressHooks(msg)
    assert sent[0][1]["status"] == c.DownloadStatusTypeFinished


def test_progressHooks_unknown_status_sends_errored(sent, request_globals):
    main.progressHooks({"status": "error"})
    assert sent[0][1]["status"] == c.DownloadStatusTypeErrored
    assert "Unknown progress status: error" in sent[0][1]["errorMsg"]


def test_progressHooks_cancels_stalled_download(monkeypatch, sent, request_globals):
    now = [dt.datetime(2026, 11, 3, 21, 0, 0)]
    monkeypatch.setattr(main.h, "getDateTimeNow", lambda: now[0])
    main.resetDownloadStalled()
    main.progressHooks(downloading("10.0%"))
    now[0] += dt.timedelta(seconds=61)

    with pytest.raises(yt_dlp.DownloadCancelled):
        main.progressHooks(downloading("10.0%"))


# --- on_message_received --------------------------------------------------------------------------------------------

class FakeChannel:
    def __init__(self):
        self.acked = []

    def basic_ack(self, delivery_tag):
        self.acked.append(delivery_tag)


def receive(body):
    channel = FakeChannel()
    main.on_message_received(channel, types.SimpleNamespace(delivery_tag=7), None, body)
    return channel


def test_version_message_reports_versions(monkeypatch, sent):
    monkeypatch.setattr(main.h, "getSoftwareVersion", lambda: "2.1.0")

    channel = receive(json.dumps({"messageType": "VERSION", "managerId": 1}))

    assert channel.acked == [7]
    message = sent[0][1]
    assert message["status"] == c.SetVersionMessageType
    assert message["downloaderApp"] == "2.1.0"
    assert message["downloaderYtdlp"] == yt_dlp.version.__version__


def test_unsupported_message_type_sends_error(sent):
    receive(json.dumps({"messageType": "NOPE", "managerId": 9}))

    assert sent[0][1] == {**sent[0][1], "managerId": 9, "status": c.DownloadStatusTypeErrored,
                          "errorMsg": "Message type not supported: NOPE"}


def test_empty_body_is_acked_and_ignored(sent):
    assert receive(b"").acked == [7]
    assert sent == []


@pytest.mark.parametrize("message_type, handler", [("DOWNLOAD", "download"), ("ANALYSIS", "analyse"), ("EXTRACT_INFO", "extract")])
def test_message_type_routes_to_handler(monkeypatch, sent, message_type, handler):
    calls = []
    monkeypatch.setattr(main, handler, lambda: calls.append(handler))

    receive(json.dumps({"messageType": message_type, "managerId": 3, "url": "u"}))

    assert calls == [handler]
    assert main._managerId == 3


# --- analyse / extract / download with a fake yt-dlp --------------------------------------------------------------

class FakeYoutubeDL:
    info = None
    error = None
    download_result = 0

    def __init__(self, opts=None):
        self.opts = opts

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False

    def extract_info(self, url, download=False):
        if FakeYoutubeDL.error:
            raise FakeYoutubeDL.error
        return FakeYoutubeDL.info

    def download(self, url):
        return FakeYoutubeDL.download_result


@pytest.fixture
def fake_ydl(monkeypatch):
    FakeYoutubeDL.info, FakeYoutubeDL.error, FakeYoutubeDL.download_result = None, None, 0
    monkeypatch.setattr(main.yt_dlp, "YoutubeDL", FakeYoutubeDL)
    return FakeYoutubeDL


VIDEO_INFO = {"id": "abc", "title": "A video", "description": None, "duration_string": "3:21",
              "thumbnail": "https://example.test/t.jpg", "url": "https://example.test/v.mp4", "resolution": "1920x1080",
              "aspect_ratio": 1.78, "filesize_approx": 123456, "live_status": "not_live",
              "formats": [{"format_id": "137", "ext": "mp4"}]}


def test_analyse_sends_video_details(sent, request_globals, fake_ydl):
    fake_ydl.info = VIDEO_INFO

    assert main.analyse() is True
    message = sent[0][1]
    assert message["status"] == c.DownloadStatusTypeAnalysis
    assert (message["videoId"], message["videoTitle"], message["videoDescription"]) == ("abc", "A video", "")
    assert (message["videoAspectRatio"], message["videoFilesizeApprox"]) == ("1.78", "123456")


def test_analyse_rejects_live_streams(sent, request_globals, fake_ydl):
    fake_ydl.info = {**VIDEO_INFO, "live_status": "is_live"}

    assert main.analyse() is False
    assert sent[0][1]["errorMsg"] == "Live stream videos not supported."


def test_analyse_logs_extract_exception_and_reports_failure(sent, request_globals, fake_ydl, capsys):
    fake_ydl.error = yt_dlp.utils.DownloadError("Unsupported URL: https://example.test")

    assert main.analyse() is False
    assert sent[0][1]["errorMsg"] == "Analysis failed, info object returned null."
    assert "Unsupported URL" in capsys.readouterr().out    # the exception used to be passed as printLog's flag and lost


def test_extract_sends_formats(sent, request_globals, fake_ydl):
    fake_ydl.info = VIDEO_INFO

    main.extract()

    assert sent[0][1] == {"managerId": 42, "extractInfo": {"formats": [{"format_id": "137", "ext": "mp4"}]},
                          "status": c.RequestExtractInfoMessageType}


def test_extract_logs_exception(sent, request_globals, fake_ydl, capsys):
    fake_ydl.error = yt_dlp.utils.DownloadError("Unsupported URL")

    main.extract()

    assert sent[0][1]["errorMsg"] == "Extraction failed, info object returned null."
    assert "Unsupported URL" in capsys.readouterr().out


def test_download_error_code_is_sent_as_text(sent, request_globals, fake_ydl):
    fake_ydl.info = VIDEO_INFO
    fake_ydl.download_result = 1

    main.download()

    error = sent[-1][1]
    assert error["status"] == c.DownloadStatusTypeErrored
    # DownloadUpdateDto.ErrorMessage is a C# string; a JSON number there makes the updater's deserialiser throw.
    assert isinstance(error["errorMsg"], str)
    assert "1" in error["errorMsg"]


def test_download_success_sends_no_error(sent, request_globals, fake_ydl):
    fake_ydl.info = VIDEO_INFO

    main.download()

    assert [m["status"] for _, m in sent] == [c.DownloadStatusTypeAnalysis]


def test_cancelled_download_removes_partial_files(monkeypatch, sent, request_globals, fake_ydl, tmp_path):
    fake_ydl.info = VIDEO_INFO
    removed = []
    monkeypatch.setattr(main, "clearCancelledFiles", lambda path, name: removed.append((path, name)))

    def cancel(self, url):
        raise yt_dlp.DownloadCancelled("Downloaded cancelled after 60 seconds at 10.0%")
    monkeypatch.setattr(FakeYoutubeDL, "download", cancel)

    main.download()

    assert removed == [("h-drive", "3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    assert "DownloadCancelled" in sent[-1][1]["errorMsg"]


def test_clearCancelledFiles_removes_only_matching_files(tmp_path):
    for name in ("abc.mp4.part", "abc.f137.mp4", "other.mp4"):
        (tmp_path / name).write_text("x")

    main.clearCancelledFiles(str(tmp_path), "abc")

    assert sorted(p.name for p in tmp_path.iterdir()) == ["other.mp4"]
