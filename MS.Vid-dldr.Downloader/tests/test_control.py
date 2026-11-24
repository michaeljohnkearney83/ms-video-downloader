"""Pause / cancel: the request registry, control messages, and how the worker stops."""
import json
import types

import pytest
import yt_dlp

import control
import main
from observability import health


class Clock:
    def __init__(self, now=1000.0):
        self.now = now

    def __call__(self):
        return self.now


# --- registry and messages ------------------------------------------------------------------------------------------

def test_a_request_is_taken_once():
    registry = control.ControlRequests()
    registry.request(15, control.PAUSE)

    assert registry.take(15) == control.PAUSE
    assert registry.take(15) is None


def test_clear_forgets_a_request():
    registry = control.ControlRequests()
    registry.request(15, control.CANCEL)
    registry.clear(15)

    assert registry.take(15) is None


def test_requests_expire_so_a_stale_one_cannot_stop_a_later_job():
    clock = Clock()
    registry = control.ControlRequests(clock=clock)
    registry.request(15, control.PAUSE)
    clock.now += control.EXPIRY_SECONDS + 1

    assert registry.take(15) is None


def test_handle_message_records_a_valid_request():
    registry = control.ControlRequests()

    result = control.handle_message(json.dumps({"messageType": "CONTROL", "action": "pause", "managerId": 7}), registry)

    assert result == (7, control.PAUSE, None)
    assert registry.pending() == {7: control.PAUSE}


@pytest.mark.parametrize("body", [
    b"not json",
    json.dumps({"messageType": "CONTROL", "action": "STOP", "managerId": 7}),
    json.dumps({"messageType": "DOWNLOAD", "action": "PAUSE", "managerId": 7}),
    json.dumps({"messageType": "CONTROL", "action": "PAUSE"}),
])
def test_handle_message_ignores_anything_else(body):
    registry = control.ControlRequests()

    assert control.handle_message(body, registry) is None
    assert registry.pending() == {}


def test_stop_is_a_yt_dlp_download_cancelled_so_yt_dlp_lets_it_through():
    stop = control.StopRequested(control.PAUSE, 3)

    assert isinstance(stop, yt_dlp.utils.DownloadCancelled)
    assert stop.action == control.PAUSE


# --- the worker -----------------------------------------------------------------------------------------------------

@pytest.fixture
def registry(monkeypatch):
    fresh = control.ControlRequests()
    monkeypatch.setattr(control, "requests", fresh)
    return fresh


@pytest.fixture
def worker(monkeypatch):
    w = health.WorkerHealth()
    w.set_consuming(True)
    monkeypatch.setattr(main.health, "worker", w)
    return w


def receive(body):
    channel = types.SimpleNamespace(basic_ack=lambda delivery_tag: None)
    main.on_message_received(channel, types.SimpleNamespace(delivery_tag=1), None, json.dumps(body))


DOWNLOAD = {"messageType": "DOWNLOAD", "managerId": 42, "downloadId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301", "url": "u"}


def test_progress_hook_stops_a_running_download(registry, request_globals, sent):
    registry.request(42, control.PAUSE)

    with pytest.raises(control.StopRequested) as stop:
        main.progressHooks({"status": "downloading", "_percent_str": "10%", "_eta_str": "01:00"})

    assert stop.value.action == control.PAUSE
    assert sent == []  # stopped before sending progress


@pytest.mark.parametrize("action, files_left", [(control.PAUSE, ["3f2504e0-4f89-11d3-9a0c-0305e82c3301.mp4.part"]),
                                                (control.CANCEL, [])])
def test_pause_keeps_the_partial_file_and_cancel_deletes_it(monkeypatch, tmp_path, registry, worker, sent, action, files_left):
    (tmp_path / "h-drive").mkdir()
    (tmp_path / "h-drive" / "3f2504e0-4f89-11d3-9a0c-0305e82c3301.mp4.part").write_text("partial")
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(main, "analyse", lambda: True)

    class StoppingYoutubeDL:
        def __init__(self, options):
            pass

        def __enter__(self):
            return self

        def __exit__(self, *exc):
            return False

        def download(self, url):
            registry.request(42, action)
            main.progressHooks({"status": "downloading", "_percent_str": "10%", "_eta_str": "01:00"})

    monkeypatch.setattr(main.yt_dlp, "YoutubeDL", StoppingYoutubeDL)

    receive(DOWNLOAD)

    assert sorted(p.name for p in (tmp_path / "h-drive").iterdir()) == files_left
    assert [m for _, m in sent if m.get("status") == "ERRORED"] == []  # no ERRORED: the Manager already knows
    assert (worker.jobs_stopped, worker.jobs_failed, worker.jobs_succeeded) == (1, 0, 0)


def test_a_queued_download_the_user_stopped_is_skipped(monkeypatch, registry, worker, sent):
    registry.request(42, control.CANCEL)
    monkeypatch.setattr(main, "download", lambda: pytest.fail("must not start"))

    receive(DOWNLOAD)

    assert worker.jobs_stopped == 1
    assert registry.take(42) is None


def test_a_resume_clears_an_old_pause_and_continues_the_partial_file(monkeypatch, registry, worker, sent):
    registry.request(42, control.PAUSE)  # e.g. the download finished before the pause arrived
    options = []
    monkeypatch.setattr(main, "download", lambda: options.append(main.getYtdlpOptions()))

    receive({**DOWNLOAD, "resume": True})

    assert options and options[0]["continuedl"] is True and options[0]["overwrites"] is False
    assert registry.take(42) is None


@pytest.mark.parametrize("running, files_left", [(None, []), (99, ["abc.mp4.part"])])
def test_cancelling_a_paused_download_deletes_its_partial_file_unless_it_is_running(monkeypatch, tmp_path, worker, running, files_left):
    (tmp_path / "h-drive").mkdir()
    (tmp_path / "h-drive" / "abc.mp4.part").write_text("partial")
    monkeypatch.chdir(tmp_path)
    if running:
        worker.job_started("DOWNLOAD", running)

    main.onControlRequest(99, control.CANCEL, "abc")

    assert sorted(p.name for p in (tmp_path / "h-drive").iterdir()) == files_left


def test_pausing_never_deletes_files(monkeypatch, tmp_path, worker):
    (tmp_path / "h-drive").mkdir()
    (tmp_path / "h-drive" / "abc.mp4.part").write_text("partial")
    monkeypatch.chdir(tmp_path)

    main.onControlRequest(99, control.PAUSE, "abc")

    assert (tmp_path / "h-drive" / "abc.mp4.part").exists()


def test_a_new_download_starts_clean(request_globals):
    options = main.getYtdlpOptions()

    assert options["overwrites"] is True and options["continuedl"] is False
