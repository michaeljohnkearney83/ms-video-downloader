"""Worker health: the /health report, the endpoint, the stuck-job watchdog, and main's job tracking."""
import json
import types
import urllib.error
import urllib.request

import pytest

import main
from observability import health


class Clock:
    def __init__(self, now=1000.0):
        self.now = now

    def __call__(self):
        return self.now

    def advance(self, seconds):
        self.now += seconds


@pytest.fixture
def clock():
    return Clock()


@pytest.fixture
def worker(clock):
    w = health.WorkerHealth(stuck_after_seconds=600, version="2.1.0", ytdlp_version="2026.10.26", clock=clock)
    w.set_consuming(True)
    return w


def test_idle_and_consuming_is_healthy(worker):
    report = worker.report()

    assert report["status"] == health.HEALTHY
    assert report["description"] == "Idle, waiting for requests"
    assert report["ytdlpVersion"] == "2026.10.26"


def test_not_consuming_is_unhealthy_after_the_start_up_grace(clock):
    w = health.WorkerHealth(clock=clock)

    assert w.report()["status"] == health.HEALTHY  # still starting
    clock.advance(health.STARTUP_GRACE_SECONDS + 1)
    report = w.report()

    assert report["status"] == health.UNHEALTHY
    assert report["description"] == "Not consuming the request queue"


def test_busy_is_healthy_and_says_what_it_is_doing(worker, clock):
    worker.job_started("DOWNLOAD", 42, "abc")
    clock.advance(200)
    worker.touch()
    clock.advance(5)

    report = worker.report()

    assert report["status"] == health.HEALTHY
    assert report["description"] == "Busy for 3m on DOWNLOAD 42 (last progress 5s ago)"
    assert report["job"] == {"type": "DOWNLOAD", "managerId": 42, "clientId": "abc", "busySeconds": 205,
                             "secondsWithoutProgress": 5}


def test_no_progress_past_the_threshold_is_stuck(worker, clock):
    worker.job_started("ANALYSIS", 7)
    clock.advance(601)

    report = worker.report()

    assert report["status"] == health.UNHEALTHY
    assert report["description"] == "Stuck: ANALYSIS 7 has shown no progress for 10m"


def test_progress_keeps_a_long_job_healthy(worker, clock):
    worker.job_started("DOWNLOAD", 1)
    for _ in range(10):
        clock.advance(500)
        worker.touch()

    assert worker.report()["status"] == health.HEALTHY


def test_jobs_are_counted_with_the_last_failure(worker, clock):
    worker.job_started("DOWNLOAD", 1)
    worker.job_finished()
    clock.advance(10)
    worker.job_started("DOWNLOAD", 2)
    worker.job_failed("HTTP Error 403")
    worker.job_finished()
    clock.advance(3)

    report = worker.report()

    assert (report["jobsSucceeded"], report["jobsFailed"]) == (1, 1)
    assert report["lastFailure"] == "HTTP Error 403"
    assert (report["lastSuccessSecondsAgo"], report["lastFailureSecondsAgo"]) == (13, 3)
    assert "job" not in report


def _get(port, path="/health"):
    try:
        with urllib.request.urlopen(f"http://127.0.0.1:{port}{path}", timeout=5) as response:
            return response.status, json.loads(response.read())
    except urllib.error.HTTPError as e:
        return e.code, (json.loads(e.read()) if e.code == 503 else None)


def test_endpoint_returns_200_when_healthy_and_503_when_stuck(worker, clock):
    server = health.start_server(worker, 0, host="127.0.0.1")
    port = server.server_address[1]
    try:
        assert _get(port) == (200, worker.report())

        worker.job_started("DOWNLOAD", 5)
        clock.advance(700)
        status, body = _get(port)

        assert status == 503
        assert body["status"] == health.UNHEALTHY
        assert _get(port, "/other")[0] == 404
    finally:
        server.shutdown()


def test_watchdog_reports_the_stuck_job_then_exits(worker, clock):
    reported, exits = [], []
    worker.job_started("DOWNLOAD", 9)
    clock.advance(901)

    acted = health.check_stuck(worker, 900, reported.append, exits.append)

    assert acted
    assert reported == ["Downloader restarted: no progress for 15m"]
    assert exits == [1]


@pytest.mark.parametrize("exit_after, quiet", [(900, 899), (0, 5000)])
def test_watchdog_leaves_a_progressing_job_alone_or_is_off(worker, clock, exit_after, quiet):
    worker.job_started("DOWNLOAD", 9)
    clock.advance(quiet)

    assert not health.check_stuck(worker, exit_after, pytest.fail, pytest.fail)


def test_watchdog_ignores_an_idle_worker(worker, clock):
    clock.advance(10_000)

    assert not health.check_stuck(worker, 900, pytest.fail, pytest.fail)


# --- main records jobs on the worker health -----------------------------------------------------------------------

@pytest.fixture
def tracked(monkeypatch, worker, sent):
    monkeypatch.setattr(main.health, "worker", worker)
    return worker


def receive(body):
    channel = types.SimpleNamespace(basic_ack=lambda delivery_tag: None)
    main.on_message_received(channel, types.SimpleNamespace(delivery_tag=1), None, body)


def test_main_counts_a_successful_job(monkeypatch, tracked):
    monkeypatch.setattr(main, "extract", lambda: None)

    receive(json.dumps({"messageType": "EXTRACT_INFO", "managerId": 5, "url": "u"}))

    assert (tracked.jobs_succeeded, tracked.jobs_failed, tracked.job) == (1, 0, None)


def test_main_counts_an_errored_job_with_its_message(tracked):
    receive(json.dumps({"messageType": "NOPE", "managerId": 9}))

    assert (tracked.jobs_succeeded, tracked.jobs_failed) == (0, 1)
    assert tracked.last_failure == "Message type not supported: NOPE"


def test_version_messages_are_not_jobs(monkeypatch, tracked):
    monkeypatch.setattr(main.h, "getSoftwareVersion", lambda: "2.1.0")

    receive(json.dumps({"messageType": "VERSION", "managerId": 1}))

    assert (tracked.jobs_succeeded, tracked.jobs_failed) == (0, 0)


def test_progress_hooks_count_as_activity(tracked, clock, request_globals):
    tracked.job_started("DOWNLOAD", 42)
    clock.advance(500)

    main.progressHooks({"status": "finished"})

    assert tracked.seconds_without_progress() == 0
