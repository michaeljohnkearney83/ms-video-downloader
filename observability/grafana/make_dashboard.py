"""Generates dashboards/ms-vid-dldr.json, the Grafana dashboard that otel-lgtm provisions (see dashboards.yaml).

Edit this file, not the JSON, then run it and commit both:

    python observability/grafana/make_dashboard.py

Grafana's newer layout hides panels inside provisioned row panels, so sections are comments, not rows.
Colours: the reference dark palette (categorical slots validated for colour-blind separation on the dark surface) and
reserved status colours (good / warning / critical) for health and ERRORED.
"""
import json, pathlib

PROM = {"type": "prometheus", "uid": "prometheus"}
LOKI = {"type": "loki", "uid": "loki"}
TEMPO = {"type": "tempo", "uid": "tempo"}

# Reference palette, dark steps (validated: all six checks pass on the dark surface).
BLUE, ORANGE, AQUA, VIOLET, MAGENTA = "#3987e5", "#d95926", "#199e70", "#9085e9", "#d55181"
CATEGORICAL = [BLUE, ORANGE, AQUA, VIOLET, MAGENTA]
# Status palette: reserved for states, never reused for a plain series.
GOOD, WARNING, CRITICAL = "#0ca30c", "#fab219", "#d03b3b"

MANAGER = 'service_name="ms-vid-dldr-manager"'
panels = []
_id = 0


def panel(kind, title, grid, targets, datasource=PROM, field_config=None, options=None, description="",
          interval=None, transformations=None):
    global _id
    _id += 1
    p = {"id": _id, "type": kind, "title": title, "gridPos": grid, "datasource": datasource, "targets": targets,
         "fieldConfig": field_config or {"defaults": {}, "overrides": []}, "options": options or {}}
    if description:
        p["description"] = description
    if interval:
        # Counters are exported every 60 s; increase() over a bucket needs two samples, so buckets are >= 2 minutes.
        p["interval"] = interval
    if transformations:
        p["transformations"] = transformations
    panels.append(p)


def row(title, y):
    """Section marker only: Grafana's newer layout hides panels under provisioned rows, so no row panel is added."""


def prom(expr, legend="", ref="A", instant=False):
    return {"refId": ref, "datasource": PROM, "expr": expr, "legendFormat": legend, "range": not instant, "instant": instant}


def color_by_name(mapping):
    return [{"matcher": {"id": "byName", "options": name},
             "properties": [{"id": "color", "value": {"mode": "fixed", "fixedColor": color}}]} for name, color in mapping.items()]


def timeseries(unit, overrides=(), bars=False, stacked=False, palette=True):
    custom = {"lineWidth": 2, "fillOpacity": 10, "showPoints": "never", "spanNulls": True,
              "axisSoftMin": 0, "gradientMode": "none", "drawStyle": "line"}
    if bars:
        custom.update({"drawStyle": "bars", "fillOpacity": 85, "lineWidth": 1, "barAlignment": 0})
    if stacked:
        custom["stacking"] = {"mode": "normal", "group": "A"}
    defaults = {"unit": unit, "custom": custom}
    if palette:
        defaults["color"] = {"mode": "palette-classic"}
    return {"defaults": defaults, "overrides": list(overrides)}


TS_OPTIONS = {"legend": {"displayMode": "list", "placement": "bottom", "showLegend": True},
              "tooltip": {"mode": "multi", "sort": "desc"}}


def stat(title, x, expr, unit, description, thresholds=None, decimals=None):
    defaults = {"unit": unit, "color": {"mode": "thresholds"},
                "thresholds": {"mode": "absolute", "steps": thresholds or [{"color": "text", "value": None}]}}
    if decimals is not None:
        defaults["decimals"] = decimals
    panel("stat", title, {"x": x, "y": 1, "w": 6, "h": 4}, [prom(expr, instant=True)],
          field_config={"defaults": defaults, "overrides": []},
          options={"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
                   "colorMode": "value", "graphMode": "none", "textMode": "value", "justifyMode": "center"},
          description=description)


# --- Health --------------------------------------------------------------------------------------------------------
panel("state-timeline", "Health (Manager /health/ready)", {"x": 0, "y": 0, "w": 24, "h": 6},
      [prom('max by (vd_health_check) (vd_health_status)', "{{vd_health_check}}")],
      field_config={"defaults": {
          # Colours come from the value mappings, so the legend names the states instead of showing thresholds.
          "color": {"mode": "fixed", "fixedColor": "text"},
          "mappings": [{"type": "value", "options": {
              "0": {"text": "Unhealthy", "color": CRITICAL, "index": 0},
              "1": {"text": "Degraded", "color": WARNING, "index": 1},
              "2": {"text": "Healthy", "color": GOOD, "index": 2}}}],
          "custom": {"fillOpacity": 85, "lineWidth": 0}}, "overrides": []},
      options={"showValue": "never", "mergeValues": True, "rowHeight": 0.8, "alignValue": "left",
               "legend": {"showLegend": True, "displayMode": "list", "placement": "bottom"},
               "tooltip": {"mode": "single"}},
      description="One row per check: database, masstransit-bus (RabbitMQ), downloader / updater (a consumer on their "
                  "queue), overall. Status colours carry the Healthy / Degraded / Unhealthy label in the legend and tooltip. "
                  "Alert rule: Video downloader dependency not healthy.")
HEALTH_HEIGHT = 6

# --- Headline ------------------------------------------------------------------------------------------------------
row("Headline", 0)
stat("Downloads requested (24h)", 0, "sum(increase(vd_downloads_requested_total[24h]))", "short",
     "Downloads the Manager queued for the Downloader in the last 24 hours.", decimals=0)
stat("Downloader errors (24h)", 6, "sum(increase(vd_downloader_errors_total[24h]))", "short",
     "ERRORED updates the Downloader sent. Each one is a failed analyse/download attempt (retries included).",
     thresholds=[{"color": "text", "value": None}, {"color": CRITICAL, "value": 1}], decimals=0)
stat("Manager p95 latency (5m)", 12,
     f"histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{{{MANAGER}}}[5m])))", "s",
     "95th percentile of all Manager REST and gRPC requests over the last 5 minutes.",
     thresholds=[{"color": "text", "value": None}, {"color": WARNING, "value": 0.5}, {"color": CRITICAL, "value": 2}])
stat("Updater p95 forward time (5m)", 18,
     "histogram_quantile(0.95, sum by (le) (rate(vd_updater_processing_duration_seconds_bucket[5m])))", "s",
     "Time for the DownloadUpdater to forward one progress message to the Manager.",
     thresholds=[{"color": "text", "value": None}, {"color": WARNING, "value": 0.5}, {"color": CRITICAL, "value": 2}])

# --- Manager API -----------------------------------------------------------------------------------------------------
row("Manager API (rate and duration)", 5)
panel("timeseries", "Requests per second, top 5 routes", {"x": 0, "y": 6, "w": 12, "h": 8},
      [prom(f"topk(5, sum by (http_route) (rate(http_server_request_duration_seconds_count{{{MANAGER}}}[$__rate_interval])))", "{{http_route}}")],
      field_config=timeseries("reqps"), options=TS_OPTIONS,
      description="gRPC errors still return HTTP 200 (the gRPC status is in a trailer), so failures show in traces and logs, not here.")
panel("timeseries", "p95 duration, top 5 routes", {"x": 12, "y": 6, "w": 12, "h": 8},
      [prom(f"topk(5, histogram_quantile(0.95, sum by (le, http_route) (rate(http_server_request_duration_seconds_bucket{{{MANAGER}}}[$__rate_interval]))))", "{{http_route}}")],
      field_config=timeseries("s"), options=TS_OPTIONS)

# --- Pipeline --------------------------------------------------------------------------------------------------------
row("Download pipeline", 14)
panel("timeseries", "Status updates received by the Manager", {"x": 0, "y": 15, "w": 12, "h": 8},
      [prom("sum by (vd_download_status) (increase(vd_download_status_updates_total[$__interval]))", "{{vd_download_status}}")],
      field_config=timeseries("short", bars=True, stacked=True, overrides=color_by_name({
          "ANALYSIS": BLUE, "DOWNLOADING": AQUA, "EXTRACT_INFO": VIOLET, "FINISHED": GOOD, "ERRORED": CRITICAL})),
      options=TS_OPTIONS, interval="2m",
      description="Per 2-minute bucket. Colour follows the status: green FINISHED and red ERRORED are status colours; the rest are series colours.")
panel("timeseries", "Request messages handled by the Downloader", {"x": 12, "y": 15, "w": 12, "h": 8},
      [prom("sum by (vd_message_type) (increase(vd_downloader_messages_total[$__interval]))", "{{vd_message_type}}")],
      field_config=timeseries("short", bars=True, stacked=True, overrides=color_by_name({
          "DOWNLOAD": BLUE, "ANALYSIS": ORANGE, "EXTRACT_INFO": AQUA, "VERSION": VIOLET})),
      options=TS_OPTIONS, interval="2m", description="Per 2-minute bucket.")
panel("timeseries", "DownloadUpdater forward time", {"x": 0, "y": 23, "w": 12, "h": 8},
      [prom("histogram_quantile(0.50, sum by (le) (rate(vd_updater_processing_duration_seconds_bucket[$__rate_interval])))", "p50", "A"),
       prom("histogram_quantile(0.95, sum by (le) (rate(vd_updater_processing_duration_seconds_bucket[$__rate_interval])))", "p95", "B")],
      field_config=timeseries("s", overrides=color_by_name({"p50": BLUE, "p95": ORANGE})), options=TS_OPTIONS)
panel("timeseries", "Postgres p95 operation time (Manager)", {"x": 12, "y": 23, "w": 12, "h": 8},
      [prom("histogram_quantile(0.95, sum by (le) (rate(db_client_operation_duration_seconds_bucket[$__rate_interval])))", "p95")],
      field_config=timeseries("s", overrides=color_by_name({"p95": BLUE})), options=TS_OPTIONS)

panel("timeseries", "Downloader jobs failing (share of jobs, 15m)", {"x": 0, "y": 31, "w": 24, "h": 8},
      [prom('sum(increase(vd_downloader_errors_total[15m])) / clamp_min(sum(increase(vd_downloader_messages_total{vd_message_type=~"DOWNLOAD|ANALYSIS|EXTRACT_INFO"}[15m])), 1)', "failing")],
      field_config={"defaults": {
          "unit": "percentunit", "min": 0, "max": 1,
          "color": {"mode": "fixed", "fixedColor": CRITICAL},
          "custom": {"lineWidth": 2, "fillOpacity": 10, "showPoints": "never", "spanNulls": True,
                     "thresholdsStyle": {"mode": "dashed"}},
          # The alert threshold, drawn as a line: status colour for the line, not for the series.
          "thresholds": {"mode": "absolute", "steps": [{"color": "transparent", "value": None}, {"color": WARNING, "value": 0.5}]}},
          "overrides": []},
      options={"legend": {"showLegend": False}, "tooltip": {"mode": "single"}},
      description="ERRORED jobs as a share of download / analyse / extract jobs over 15 minutes (retries included). "
                  "Dashed line: the 50% alert threshold (alert: Video downloader jobs mostly failing, needs at least 3 jobs).")

# --- Errors and traces -----------------------------------------------------------------------------------------------
row("Errors and traces", 31)
panel("logs", "Error logs, all services (Loki)", {"x": 0, "y": 39, "w": 24, "h": 9},
      [{"refId": "A", "datasource": LOKI, "expr": '{service_name=~"ms-vid-dldr.+"} | detected_level=~"error|critical|fatal"'}],
      datasource=LOKI,
      options={"showTime": True, "showLabels": False, "wrapLogMessage": True, "sortOrder": "Descending",
               "enableLogDetails": True, "dedupStrategy": "none"},
      description="Open a line and use the trace_id link to jump to the trace in Tempo.")
panel("table", "Failed traces (Tempo)", {"x": 0, "y": 48, "w": 24, "h": 9},
      [{"refId": "A", "datasource": TEMPO, "queryType": "traceql", "limit": 20,
        "query": '{resource.service.name=~"ms-vid-dldr.+" && status = error}'}],
      datasource=TEMPO,
      transformations=[{"id": "organize", "options": {"excludeByName": {"nested": True}}}],
      description="Traces with a failed span. Click a trace id to open the whole download across services. "
                  "'<root span not yet received>' means the trace started in the browser, which sends a traceparent "
                  "but exports no span of its own.")

# Everything after the health timeline moves down by its height.
for p in panels[1:]:
    p["gridPos"]["y"] += HEALTH_HEIGHT

dashboard = {
    "uid": "ms-vid-dldr-overview",
    "title": "Video downloader - overview",
    "tags": ["ms-vid-dldr", "opentelemetry"],
    "timezone": "browser",
    "schemaVersion": 39,
    "version": 1,
    "refresh": "30s",
    "time": {"from": "now-1h", "to": "now"},
    "editable": True,
    "graphTooltip": 1,
    "panels": panels,
    "links": [{"title": "Explore traces", "type": "link", "url": "/explore", "targetBlank": False}],
}

out = pathlib.Path(__file__).resolve().parent / "dashboards" / "ms-vid-dldr.json"
out.write_text(json.dumps(dashboard, indent=2) + "\n", encoding="utf-8", newline="\n")
print(out, len(panels), "panels")
