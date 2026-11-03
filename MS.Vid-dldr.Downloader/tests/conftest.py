import json
import os
import sys

import pytest

# The app modules import each other by bare name ("import helper as h"), as they do when run from app/.
APP_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "app")
sys.path.insert(0, APP_DIR)

import appsettings  # noqa: E402
import main  # noqa: E402


@pytest.fixture(autouse=True)
def default_settings(monkeypatch):
    """Restore appsettings defaults and clear env overrides around every test."""
    defaults = {
        "IsDebugMode": True,
        "QueueRequestDownload": "ms_vd_q_downloadRequest",
        "QueueDownloadUpdate": "ms_vd_q_downloadUpdate",
        "ChannelHost": "rabbitmq",
        "ChannelPortNumber": 5672,
        "DefaultDownloadExt": "mp4",
        "DefaultRetryCount": 3,
        "DefaultTimeoutSeconds": 30,
        "DefaultDownloadStalledTimeoutSeconds": 138,
    }
    for name, value in defaults.items():
        monkeypatch.setattr(appsettings, name, value)
    for var in ("IS_DEBUG_MODE", "QUEUE_REQUEST_DOWNLOAD", "QUEUE_DOWNLOAD_UPDATE", "CHANNEL_HOST",
                "CHANNEL_PORT_NUMBER", "DEFAULT_DOWNLOAD_EXT", "DEFAULT_RETRY_COUNT", "DEFAULT_TIMEOUT_SECONDS",
                "DEFAULT_DOWNLOAD_STALLED_TIMEOUT_SECONDS"):
        monkeypatch.delenv(var, raising=False)


@pytest.fixture
def sent(monkeypatch):
    """Replaces the RabbitMQ producer; returns the list of (queue, message dict) that would have been sent."""
    messages = []
    monkeypatch.setattr(main.dlUpdates, "Send",
                        lambda queue, body, host, port: messages.append((queue, json.loads(body))))
    return messages


@pytest.fixture
def request_globals():
    """Sets main's per-request globals the way a DOWNLOAD message from the Manager does."""
    main.setGlobals({
        "messageType": "DOWNLOAD",
        "managerId": 42,
        "downloadId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
        "url": "https://example.test/watch?v=abc",
        "downloadName": "My video",
        "downloadExt": "mp4",
        "retryCount": "5",
        "timeoutSeconds": "20",
        "downloadStalledTimeoutSeconds": "60",
    })
