"""Trace hand-off: Manager -> (traceparent header) -> Downloader -> (traceparent header) -> DownloadUpdater."""
import json
import types

import pytest
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import SimpleSpanProcessor
from opentelemetry.sdk.trace.export.in_memory_span_exporter import InMemorySpanExporter
from opentelemetry.trace import SpanKind, StatusCode

import main
from observability import telemetry

TRACE_ID = "0af7651916cd43dd8448eb211c80319c"
PARENT_SPAN_ID = "b7ad6b7169203331"
TRACEPARENT = f"00-{TRACE_ID}-{PARENT_SPAN_ID}-01"

_exporter = InMemorySpanExporter()


@pytest.fixture(scope="module", autouse=True)
def tracer_provider():
    # The global provider can be set once per process; these tests are the only ones that need one.
    if not isinstance(trace.get_tracer_provider(), TracerProvider):
        provider = TracerProvider()
        provider.add_span_processor(SimpleSpanProcessor(_exporter))
        trace.set_tracer_provider(provider)


@pytest.fixture(autouse=True)
def clear_spans():
    _exporter.clear()


@pytest.fixture
def sent_with_headers(monkeypatch):
    """Like the `sent` fixture, but also records the trace headers the producer would add."""
    messages = []
    monkeypatch.setattr(main.dlUpdates, "Send",
                        lambda queue, body, host, port: messages.append((json.loads(body), telemetry.inject())))
    return messages


def receive(body, headers):
    channel = types.SimpleNamespace(basic_ack=lambda delivery_tag: None)
    main.on_message_received(channel, types.SimpleNamespace(delivery_tag=1), types.SimpleNamespace(headers=headers), body)


def spans_named(prefix):
    return [s for s in _exporter.get_finished_spans() if s.name.startswith(prefix)]


def test_consumer_span_continues_the_managers_trace(monkeypatch, sent_with_headers):
    monkeypatch.setattr(main, "extract", lambda: None)

    receive(json.dumps({"messageType": "EXTRACT_INFO", "managerId": 5, "url": "u"}), {"traceparent": TRACEPARENT})

    [span] = spans_named("ms_vd_q_downloadRequest process")
    assert span.kind == SpanKind.CONSUMER
    assert format(span.context.trace_id, "032x") == TRACE_ID
    assert format(span.parent.span_id, "016x") == PARENT_SPAN_ID
    assert span.attributes[telemetry.DOWNLOAD_ID] == 5
    assert span.attributes[telemetry.MESSAGE_TYPE] == "EXTRACT_INFO"


def test_progress_messages_carry_the_same_trace(sent_with_headers):
    receive(json.dumps({"messageType": "NOPE", "managerId": 9}), {"traceparent": TRACEPARENT})

    _message, headers = sent_with_headers[0]
    assert headers["traceparent"].split("-")[1] == TRACE_ID


def test_error_update_marks_the_span_failed(sent_with_headers):
    receive(json.dumps({"messageType": "NOPE", "managerId": 9}), None)

    [span] = spans_named("ms_vd_q_downloadRequest process")
    assert span.status.status_code == StatusCode.ERROR
    assert "Message type not supported" in span.status.description


def test_without_traceparent_a_new_trace_starts(monkeypatch, sent_with_headers):
    monkeypatch.setattr(main, "extract", lambda: None)

    receive(json.dumps({"messageType": "EXTRACT_INFO", "managerId": 5, "url": "u"}), None)

    [span] = spans_named("ms_vd_q_downloadRequest process")
    assert span.parent is None


def test_traced_records_exceptions():
    @telemetry.traced("boom")
    def boom():
        raise ValueError("bad")

    with pytest.raises(ValueError):
        boom()

    [span] = spans_named("boom")
    assert span.status.status_code == StatusCode.ERROR
    assert span.events[0].name == "exception"


def test_extract_context_accepts_bytes_header_values():
    context = telemetry.extract_context({"traceparent": TRACEPARENT.encode()})

    assert format(trace.get_current_span(context).get_span_context().trace_id, "032x") == TRACE_ID
