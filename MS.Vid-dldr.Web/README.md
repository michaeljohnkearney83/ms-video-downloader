# MS.Vid-dldr.Web

A React + TypeScript front end for the **Manager** API, part of this repository (see the root README). It calls the Manager's REST endpoints (gRPC JSON transcoding); the Manager stores the request and sends
it to the Downloader over RabbitMQ with MassTransit.

```
React app ──REST──> Manager ──MassTransit/RabbitMQ──> Downloader (yt-dlp) ──> DownloadUpdater ──gRPC──> Manager
    ^                  │
    └──── polls ───────┘  /v1/client/downloads/{sessionId}
```

## What it does

| Component | Manager endpoint |
| --- | --- |
| `ServiceStatus` – every 15 s: "All systems up" / "Degraded" / "Down" / "Manager unreachable", one chip per dependency (✓ Database, ✓ Queue, ! Downloader, ✓ Updater; icon and label, not colour alone) and the reason when something isn't healthy | `GET /health/ready` (503 when Unhealthy still carries the report) |
| `SessionForm` – gets a download session for user, FID, client name and deployment no. (remembered in `localStorage`). Use your Windows user name: the Distributor only delivers files for the user it runs as | `GET /v1/client/session/{username}/{fid}/{clientName}/{deploymentNo}` |
| `DownloadForm` – URL, folder, file name, extension, retries, timeouts, force | `POST /v1/client/download` |
| `DownloadForm` → `FormatsTable` – "Check formats" lists what yt-dlp found; "Use .ext" copies the extension | `POST /v1/client/download/extractInfo/formats` |
| `DownloadList` – status badge and progress bar per download, every 3 s and straight after queueing | `GET /v1/client/downloads/{sessionId}` |

`src/api/managerClient.ts` is the typed client. It throws `ManagerApiError` for network failures, gRPC errors
(`{ code, message }`) and for HTTP 200 responses whose `feedback.errorMessage` is set. Components get the client from
`ManagerClientProvider`, so tests pass a fake.

**Tracing.** Every API call starts a W3C trace: the client sends a `traceparent` header, the Manager continues it, and
the whole download (Manager, RabbitMQ, Downloader, DownloadUpdater) shares that trace id. Errors carry it
(`ManagerApiError.traceId`) and the UI shows it, e.g. "Failed to get session id (trace 5071…)". Search for it in Grafana
(Tempo or Loki) or Sentry. The browser exports no spans of its own, so in Tempo the trace's root shows as "not yet
received".

## Requirements

- Node 20+ (built with Node 24).
- A running Manager (.NET 11). From the repo root: the Dev compose stack
  (`docker compose --env-file .env.dev --env-file .env.dev.local -f docker-compose.Dev.yaml up --build -d`, REST on `http://localhost:8000`), or
  `dotnet run` in `MS.Vid-dldr.Manager` (`http://localhost:5210`) with RabbitMQ and PostgreSQL running.

## Run

From this folder (`MS.Vid-dldr.Web`):

```sh
npm install
npm run dev                                  # http://localhost:5173, proxies /v1 to http://localhost:8000
MANAGER_URL=http://localhost:5210 npm run dev   # Manager started with dotnet run
```

PowerShell: `$env:MANAGER_URL = 'http://localhost:5210'; npm run dev`. If port 5173 is taken, add `-- --port 5178`.

In development the browser only talks to Vite, which proxies `/v1` and `/health` to `MANAGER_URL`, so CORS does not come into it.

## Build

```sh
npm run build        # type-check and bundle to dist/
```

The built app calls the same origin by default. To call a Manager elsewhere, set `VITE_MANAGER_URL` at build time
(`VITE_MANAGER_URL=http://manager:8000 npm run build`); the Manager's `AllowAll` CORS policy allows it.

## Docker

The image builds the app with Node 24 and serves it with unprivileged nginx on port 8080. nginx proxies `/v1` and
`/health` to `MANAGER_URL` (default `http://manager-api:8000`), resolving it through Docker's DNS on each request, so the container
starts even before the Manager does. `GET /healthz` returns `ok` (nginx only); the compose healthcheck uses it, and
`docker compose up -d --wait` waits for it.

It is the `web` service in the root `docker-compose.Dev.yaml` (http://localhost:8080), started with the rest of the
stack and after the Manager is healthy:

```sh
docker compose --env-file .env.dev --env-file .env.dev.local -f docker-compose.Dev.yaml up --build -d --wait   # from the repo root
```

| Setting | Default | Use |
| --- | --- | --- |
| `MANAGER_URL` (container env) | `http://manager-api:8000` | Where nginx sends `/v1` and `/health` |
| `VITE_MANAGER_URL` (build arg) | empty | Make the browser call the Manager directly instead of through nginx |

## Test and lint

```sh
npm test             # vitest + Testing Library (jsdom), no Manager needed
npm run lint         # oxlint
```

| Area | Covered |
| --- | --- |
| `managerClient` | encoded path segments, JSON POST body, `isUp` with proto3 zero values left out, `feedback.errorMessage`, gRPC error body, network failure, a new W3C `traceparent` per call, trace id on errors (`describeError`), health report read from a 503, non-report health response |
| `parsePercentage` | yt-dlp text (`" 42.5%"`, `"null"`, empty), clamped to 0–100 |
| `SessionForm` | gets a session and saves the key; shows the Manager error |
| `DownloadForm` | request body, "already active" warning, formats table and "Use .ext" |
| `DownloadList` | rows, status, progress bar value, per-download error |
| `ServiceStatus` | "Manager unreachable" when the call fails; names each dependency and shows why one is degraded; "All systems up" when healthy |

## Layout

```
src/
  api/          types.ts (proto JSON shapes), managerClient.ts, ManagerClientProvider.tsx, managerClientContext.ts
  components/   Alert, DownloadForm, DownloadList, FormatsTable, ProgressBar, ServiceStatus, SessionForm, StatusBadge
  hooks/        usePolling.ts
  test/         setup.ts, fakeClient.tsx
```
