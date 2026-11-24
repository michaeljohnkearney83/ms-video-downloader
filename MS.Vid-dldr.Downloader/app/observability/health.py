"""Health of the Downloader worker: an HTTP endpoint and a stuck-job watchdog.

The Manager's queue check can only see that *something* consumes the request queue. This module tells it what the
worker is doing:

* ``GET /health`` (port ``HEALTH_PORT``, default 8081) returns JSON with the status, the current job and how long since
  it last showed progress, the last success and failure, and job counts. HTTP 200 when Healthy, 503 when Unhealthy.
  The Manager's ``downloader`` check and the Docker healthcheck read it.
* Unhealthy means: not consuming the queue (after a start-up grace period), or a job that has shown no progress for
  ``HEALTH_STUCK_AFTER_SECONDS`` (default 600). Busy is normal and stays Healthy.
* A watchdog thread handles a job with no progress for ``HEALTH_EXIT_AFTER_SECONDS`` (default 900, 0 = off): it calls
  ``on_stuck`` (main sends the Manager an ERRORED update, so its retry logic takes over), then exits the process.
  Docker restarts containers that exit, but not ones that are merely unhealthy.

The worker is single-threaded (pika BlockingConnection, one message at a time), so the endpoint and the watchdog run on
their own daemon threads and only read state the worker updates under a lock.
"""
import json
import os
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HEALTHY, DEGRADED, UNHEALTHY = "Healthy", "Degraded", "Unhealthy"

STARTUP_GRACE_SECONDS = 30


def _env_int(name, default):
    try:
        return int(os.getenv(name, default))
    except (TypeError, ValueError):
        return default


class WorkerHealth:
    """Thread-safe record of what the worker is doing. ``clock`` is injectable for tests."""

    def __init__(self, stuck_after_seconds=600, version="", ytdlp_version="", clock=time.time):
        self._lock = threading.Lock()
        self._clock = clock
        self.stuck_after_seconds = stuck_after_seconds
        self.version = version
        self.ytdlp_version = ytdlp_version
        self.started_at = clock()
        self.consuming = False
        self.job = None
        self.jobs_succeeded = 0
        self.jobs_failed = 0
        self.jobs_stopped = 0
        self.last_success_at = None
        self.last_failure_at = None
        self.last_failure = None

    # --- updated by the worker ---------------------------------------------------------------------------------

    def set_consuming(self, consuming):
        with self._lock:
            self.consuming = consuming

    def job_started(self, message_type, manager_id=None, client_id=None):
        now = self._clock()
        with self._lock:
            self.job = {"type": message_type, "managerId": manager_id, "clientId": client_id,
                        "startedAt": now, "lastActivityAt": now, "failed": False, "error": None, "stopped": None}

    def current_manager_id(self):
        with self._lock:
            return self.job["managerId"] if self.job else None

    def touch(self):
        """Progress was made (yt-dlp progress hook)."""
        with self._lock:
            if self.job:
                self.job["lastActivityAt"] = self._clock()

    def job_failed(self, message):
        """The current job sent an ERRORED update."""
        with self._lock:
            if self.job:
                self.job["failed"] = True
                self.job["error"] = str(message)

    def job_stopped(self, action):
        """The user paused or cancelled the current job: neither a success nor a failure."""
        with self._lock:
            if self.job:
                self.job["stopped"] = action

    def job_finished(self):
        now = self._clock()
        with self._lock:
            job, self.job = self.job, None
            if not job:
                return
            if job["stopped"]:
                self.jobs_stopped += 1
            elif job["failed"]:
                self.jobs_failed += 1
                self.last_failure_at = now
                self.last_failure = job["error"]
            else:
                self.jobs_succeeded += 1
                self.last_success_at = now

    # --- read by the endpoint and the watchdog -------------------------------------------------------------------

    def seconds_without_progress(self):
        with self._lock:
            return None if not self.job else self._clock() - self.job["lastActivityAt"]

    def report(self):
        now = self._clock()
        with self._lock:
            job = dict(self.job) if self.job else None
            consuming = self.consuming
            started_at = self.started_at
            snapshot = {
                "jobsSucceeded": self.jobs_succeeded,
                "jobsFailed": self.jobs_failed,
                "jobsStopped": self.jobs_stopped,
                "lastSuccessSecondsAgo": None if self.last_success_at is None else round(now - self.last_success_at),
                "lastFailureSecondsAgo": None if self.last_failure_at is None else round(now - self.last_failure_at),
                "lastFailure": self.last_failure,
                "version": self.version,
                "ytdlpVersion": self.ytdlp_version,
                "uptimeSeconds": round(now - started_at),
            }

        status, description = HEALTHY, "Idle, waiting for requests"
        if job:
            busy = now - job["startedAt"]
            quiet = now - job["lastActivityAt"]
            what = f"{job['type']} {job['managerId']}" if job["managerId"] not in (None, -1) else job["type"]
            description = f"Busy for {_human(busy)} on {what} (last progress {_human(quiet)} ago)"
            if quiet > self.stuck_after_seconds:
                status = UNHEALTHY
                description = f"Stuck: {what} has shown no progress for {_human(quiet)}"
            snapshot["job"] = {
                "type": job["type"], "managerId": job["managerId"], "clientId": job["clientId"],
                "busySeconds": round(busy), "secondsWithoutProgress": round(quiet),
            }
        if not consuming and now - started_at > STARTUP_GRACE_SECONDS:
            status, description = UNHEALTHY, "Not consuming the request queue"

        return {"status": status, "description": description, **snapshot}


def _human(seconds):
    seconds = int(seconds)
    if seconds < 90:
        return f"{seconds}s"
    if seconds < 5400:
        return f"{seconds // 60}m"
    return f"{seconds // 3600}h{(seconds % 3600) // 60:02d}m"


def start_server(health, port, host="0.0.0.0"):
    """Serves ``GET /health`` on a daemon thread. Returns the server (``server.server_address`` has the port)."""

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path.rstrip("/") not in ("/health", "/healthz"):
                self.send_error(404)
                return
            report = health.report()
            body = json.dumps(report).encode()
            self.send_response(503 if report["status"] == UNHEALTHY else 200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *args):  # Docker polls every few seconds; don't fill the log.
            pass

    server = ThreadingHTTPServer((host, port), Handler)
    threading.Thread(target=server.serve_forever, name="health-http", daemon=True).start()
    return server


def check_stuck(health, exit_after_seconds, on_stuck, exit_process):
    """One watchdog pass. Returns True when it acted. Separate from the loop so it can be tested."""
    if exit_after_seconds <= 0:
        return False
    quiet = health.seconds_without_progress()
    if quiet is None or quiet <= exit_after_seconds:
        return False
    try:
        on_stuck(f"Downloader restarted: no progress for {_human(quiet)}")
    finally:
        exit_process(1)
    return True


def start_watchdog(health, exit_after_seconds, on_stuck, exit_process=os._exit, interval_seconds=15):
    def loop():
        while True:
            time.sleep(interval_seconds)
            if check_stuck(health, exit_after_seconds, on_stuck, exit_process):
                return

    if exit_after_seconds > 0:
        threading.Thread(target=loop, name="health-watchdog", daemon=True).start()


def setup(version="", ytdlp_version="", on_stuck=lambda message: None):
    """Creates the worker health record, starts the endpoint and the watchdog from env settings."""
    health = WorkerHealth(stuck_after_seconds=_env_int("HEALTH_STUCK_AFTER_SECONDS", 600),
                          version=version, ytdlp_version=ytdlp_version)
    start_server(health, _env_int("HEALTH_PORT", 8081))
    start_watchdog(health, _env_int("HEALTH_EXIT_AFTER_SECONDS", 900), on_stuck)
    return health


class _NoOp:
    """Stand-in before setup() runs (unit tests, imports): every update is ignored."""

    def __getattr__(self, name):
        return lambda *args, **kwargs: None


worker = _NoOp()
