"""OpenTelemetry (traces, metrics, logs) and Sentry for the Downloader.

The Manager puts a W3C ``traceparent`` header on each request message. The consumer span continues that trace, and
every progress message the Downloader sends carries the current ``traceparent``, so the DownloadUpdater and the
Manager's progress updates join the same trace.

Nothing is exported until ``setup()`` runs with OTEL_EXPORTER_OTLP_ENDPOINT set (OTLP over HTTP, e.g.
http://otel-lgtm:4318). Before that the API is a no-op, which keeps the unit tests free of exporters.
Sentry starts only when SENTRY_DSN is set.
"""
import atexit
import functools
import logging
import os

from opentelemetry import metrics, trace
from opentelemetry.baggage.propagation import W3CBaggagePropagator
from opentelemetry.propagators.composite import CompositePropagator
from opentelemetry.trace import SpanKind, Status, StatusCode
from opentelemetry.trace.propagation.tracecontext import TraceContextTextMapPropagator

SERVICE_NAME = "ms-vid-dldr-downloader"

# Same attribute names as the .NET services, so one query finds a download everywhere.
DOWNLOAD_ID = "vd.download.id"
CLIENT_ID = "vd.download.client_id"
MESSAGE_TYPE = "vd.message_type"

# Explicit W3C propagator: works the same whether or not setup() has run.
_propagator = CompositePropagator([TraceContextTextMapPropagator(), W3CBaggagePropagator()])

tracer = trace.get_tracer(SERVICE_NAME)
meter = metrics.get_meter(SERVICE_NAME)
logger = logging.getLogger(SERVICE_NAME)

messages = meter.create_counter("vd.downloader.messages", description="Request messages handled, by message type")
errors = meter.create_counter("vd.downloader.errors", description="ERRORED updates sent to the Manager")


def setup(version=""):
    """Start Sentry and the OTLP exporters. Call once, at start-up."""
    dsn = os.getenv("SENTRY_DSN")
    if dsn:
        import sentry_sdk

        def tag_trace(event, _hint):
            span_context = trace.get_current_span().get_span_context()
            if span_context.is_valid:
                event.setdefault("tags", {})["otel.trace_id"] = format(span_context.trace_id, "032x")
            return event

        sentry_sdk.init(dsn=dsn, release=f"{SERVICE_NAME}@{version}", before_send=tag_trace, traces_sample_rate=0)

    if not os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT"):
        return

    from opentelemetry._logs import set_logger_provider
    from opentelemetry.exporter.otlp.proto.http._log_exporter import OTLPLogExporter
    from opentelemetry.exporter.otlp.proto.http.metric_exporter import OTLPMetricExporter
    from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
    from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
    from opentelemetry.sdk._logs.export import BatchLogRecordProcessor
    from opentelemetry.sdk.metrics import MeterProvider
    from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
    from opentelemetry.sdk.resources import Resource
    from opentelemetry.sdk.trace import TracerProvider
    from opentelemetry.sdk.trace.export import BatchSpanProcessor

    resource = Resource.create({
        "service.name": os.getenv("OTEL_SERVICE_NAME", SERVICE_NAME),
        "service.version": version,
    })

    tracer_provider = TracerProvider(resource=resource)
    tracer_provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter()))
    trace.set_tracer_provider(tracer_provider)

    meter_provider = MeterProvider(resource=resource, metric_readers=[PeriodicExportingMetricReader(OTLPMetricExporter())])
    metrics.set_meter_provider(meter_provider)

    logger_provider = LoggerProvider(resource=resource)
    logger_provider.add_log_record_processor(BatchLogRecordProcessor(OTLPLogExporter()))
    set_logger_provider(logger_provider)
    root = logging.getLogger()
    root.addHandler(LoggingHandler(level=logging.INFO, logger_provider=logger_provider))
    root.setLevel(logging.INFO)
    # pika logs every connection, and the producer opens one per progress message.
    logging.getLogger("pika").setLevel(logging.WARNING)

    # Report each counter series at 0 so Prometheus' increase() counts the first real message.
    for message_type in ("DOWNLOAD", "ANALYSIS", "EXTRACT_INFO", "VERSION"):
        messages.add(0, {MESSAGE_TYPE: message_type})
    errors.add(0)

    def shutdown():
        tracer_provider.shutdown()
        meter_provider.shutdown()
        logger_provider.shutdown()

    atexit.register(shutdown)


def extract_context(headers):
    """Trace context from a message's headers (pika gives a dict of str, or None)."""
    carrier = {k: (v.decode() if isinstance(v, bytes) else str(v)) for k, v in (headers or {}).items()}
    return _propagator.extract(carrier=carrier)


def inject(headers=None):
    """Adds the current traceparent (and baggage) to a headers dict and returns it."""
    headers = {} if headers is None else headers
    _propagator.inject(headers)
    return headers


def consume_span(queue, headers):
    """Consumer span for one request message, continuing the sender's trace."""
    return tracer.start_as_current_span(
        f"{queue} process",
        context=extract_context(headers),
        kind=SpanKind.CONSUMER,
        attributes={
            "messaging.system": "rabbitmq",
            "messaging.destination.name": queue,
            "messaging.operation.type": "process",
        },
    )


def traced(name):
    """Decorator: runs the function in a child span and records exceptions on it."""
    def decorator(fn):
        @functools.wraps(fn)
        def wrapper(*args, **kwargs):
            with tracer.start_as_current_span(name) as span:
                try:
                    return fn(*args, **kwargs)
                except Exception as e:
                    span.record_exception(e)
                    span.set_status(Status(StatusCode.ERROR, str(e)))
                    raise
        return wrapper
    return decorator


def mark_error(message):
    """Marks the current span as failed and counts the error."""
    span = trace.get_current_span()
    span.set_status(Status(StatusCode.ERROR, message))
    errors.add(1)
