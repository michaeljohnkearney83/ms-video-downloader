"""Pause / cancel requests from the Manager.

The Manager publishes ``{"messageType": "CONTROL", "action": "PAUSE" | "CANCEL", "managerId": 15, ...}`` to the fanout
exchange ``EXCHANGE_DOWNLOAD_CONTROL`` (default ``ms_vd_x_downloadControl``). Each Downloader binds its own temporary
queue to it on a background thread (the worker thread is busy inside yt-dlp), and records the request here. The worker
then:

* stops a running download at its next yt-dlp progress hook (``check_running``), and
* skips a queued download when its request message arrives (``take``).

PAUSE keeps the partial ``.part`` file so a resumed download continues it; CANCEL deletes it. Requests expire after
``EXPIRY_SECONDS`` so a stale one (the download finished before it arrived) can't stop a later job.
"""
import json
import os
import threading
import time

from yt_dlp.utils import DownloadCancelled

PAUSE, CANCEL = "PAUSE", "CANCEL"
ACTIONS = (PAUSE, CANCEL)
EXPIRY_SECONDS = 3600


class StopRequested(DownloadCancelled):
    """Raised in a yt-dlp progress hook to stop the download. A DownloadCancelled, which yt-dlp re-raises unchanged."""

    def __init__(self, action, manager_id):
        super().__init__(f"{action} requested for download {manager_id}")
        self.action = action


class ControlRequests:
    """Thread-safe pending pause / cancel requests by Manager download id. ``clock`` is injectable for tests."""

    def __init__(self, clock=time.time):
        self._lock = threading.Lock()
        self._clock = clock
        self._pending = {}

    def request(self, manager_id, action):
        if action not in ACTIONS:
            return False
        with self._lock:
            self._pending[int(manager_id)] = (action, self._clock())
        return True

    def take(self, manager_id):
        """The pending action for this download (and forget it), or None."""
        with self._lock:
            self._expire()
            entry = self._pending.pop(int(manager_id), None)
        return entry[0] if entry else None

    def clear(self, manager_id):
        """Forget any request: a resume is newer than the pause it undoes."""
        with self._lock:
            self._pending.pop(int(manager_id), None)

    def pending(self):
        with self._lock:
            self._expire()
            return {k: v[0] for k, v in self._pending.items()}

    def _expire(self):
        now = self._clock()
        for key in [k for k, (_, at) in self._pending.items() if now - at > EXPIRY_SECONDS]:
            del self._pending[key]


requests = ControlRequests()


def handle_message(body, registry=None):
    """Records one control message. Returns (managerId, action, downloadId) or None when it isn't a valid request."""
    registry = registry or requests
    try:
        message = json.loads(body)
        manager_id = int(message["managerId"])
        action = str(message["action"]).upper()
    except (ValueError, KeyError, TypeError):
        return None
    if message.get("messageType") != "CONTROL" or not registry.request(manager_id, action):
        return None
    return manager_id, action, message.get("downloadId")


def start_listener(host, port, log, exchange=None, retry_seconds=5, on_request=None):
    """Consumes the control exchange on a daemon thread, reconnecting if the connection drops.
    ``on_request(managerId, action, downloadId)`` runs for each valid request (e.g. delete a paused download's files)."""
    import pika
    from observability import telemetry

    exchange = exchange or os.getenv("EXCHANGE_DOWNLOAD_CONTROL", "ms_vd_x_downloadControl")

    def on_message(channel, method, properties, body):
        with telemetry.consume_span(exchange, getattr(properties, "headers", None)):
            result = handle_message(body)
            log(f"Control request {result[1]} for download {result[0]}" if result else f"Ignored control message: {body!r}")
            if result and on_request:
                on_request(*result)

    def run():
        while True:
            try:
                connection = pika.BlockingConnection(pika.ConnectionParameters(host, port))
                channel = connection.channel()
                # Must match the Manager's (MassTransit) declaration: fanout, non-durable.
                channel.exchange_declare(exchange=exchange, exchange_type="fanout", durable=False, auto_delete=False)
                queue = channel.queue_declare(queue="", exclusive=True, auto_delete=True).method.queue
                channel.queue_bind(queue=queue, exchange=exchange)
                channel.basic_consume(queue=queue, on_message_callback=on_message, auto_ack=True)
                log(f"Listening for pause / cancel on {exchange}")
                channel.start_consuming()
            except Exception as e:  # reconnect; never let this thread die
                log(f"ERROR: control listener: {e!r}; reconnecting in {retry_seconds}s")
                time.sleep(retry_seconds)

    threading.Thread(target=run, name="control-listener", daemon=True).start()
