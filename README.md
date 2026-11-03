# MS.Video-downloader

A set of services for downloading videos with yt-dlp. Clients send download requests to the **Manager** gRPC API. The
Manager stores them in PostgreSQL and queues them on RabbitMQ. The **Downloader** picks them up and runs yt-dlp. The
**DownloadUpdater** sends progress back to the Manager. **Ytdlp** keeps yt-dlp up to date, and **Distributor** moves
finished files to their destination.

## Layout

| Folder | What | Tech |
| --- | --- | --- |
| `MS.Vid-dldr.Manager/` | gRPC API (+ JSON transcoding / Swagger), EF Core + PostgreSQL, RabbitMQ producer/consumer | .NET 7 |
| `MS.Vid-dldr.Manager.Tests/` | xUnit tests for the Manager (sibling folder so the web SDK doesn't compile them into the API) | .NET 7 |
| `MS.Vid-dldr.Downloader/` | RabbitMQ consumer that runs yt-dlp | Python 3.12 |
| `MS.Vid-dldr.DownloadUpdater/` | Forwards download progress from RabbitMQ to the Manager over gRPC | .NET 7 |
| `MS.Vid-dldr.DownloadUpdater.Tests/` | xUnit tests for the DownloadUpdater | .NET 7 |
| `MS.Vid-dldr.Ytdlp/` | Checks and reports yt-dlp versions via the Manager | .NET 9 |
| `MS.Vid-dldr.Ytdlp.Tests/` | xUnit tests for Ytdlp | .NET 9 |
| `MS.Vid-dldr.Distributor/` | Windows tool that distributes finished downloads | .NET 8 |
| `MS.Vid-dldr.Distributor.Tests/` | xUnit tests for the Distributor | .NET 8 |
| `docker-compose.{Dev,Prod,Test}.yaml` | Compose stacks; `.env.dev` / `.env.prod` hold queue names and paths | |

All services and their tests live in this one repository. Each service folder has its own `.sln`. The gRPC contracts
are the `Protos/ms_vdapi_*.proto` files, which are copied into each service; keep them in sync. The root `.gitignore`
covers build output (`bin/`, `obj/`), test results, `Logs/` and Python caches for every folder.

```sh
git clone https://github.com/michaeljohnkearney83/ms-video-downloader.git
```

Naming: folders, projects, `.sln` files, dlls and Docker images use `MS.Vid-dldr.*` / `ms-vid-dldr-*`. C# identifiers
can't contain `-`, so namespaces (and proto `csharp_namespace`) use `MS.Vid_dldr.*`, which matches the default root
namespace MSBuild derives from the project name. The compose project is `ms_vid-dldr`.

## Build

```sh
cd MS.Vid-dldr.Manager && dotnet build          # likewise for DownloadUpdater, Ytdlp, Distributor
```

## Test

```sh
dotnet test MS.Vid-dldr.Manager/MS.Vid-dldr.Manager.sln
dotnet test MS.Vid-dldr.DownloadUpdater/MS.Vid-dldr.DownloadUpdater.sln
dotnet test MS.Vid-dldr.Distributor/MS.Vid-dldr.Distributor.sln
dotnet test MS.Vid-dldr.Ytdlp/MS.Vid-dldr.Ytdlp.sln

cd MS.Vid-dldr.Downloader
python -m venv .venv && . .venv/bin/activate     # Windows: .venv\Scripts\activate
pip install -r requirements-dev.txt
python -m pytest
```

Each .NET test project sits next to its service and is in that service's `.sln`. They set `RollForward=Major`, so they
also run where only a newer .NET runtime is installed. The Python tests are in `MS.Vid-dldr.Downloader/tests/`, outside
`app/`, so the Dockerfiles (which copy only `app/`) leave them out of the image.

### Manager

| Area | Covered |
| --- | --- |
| `StartupRetry` | success, retry then success, give up and rethrow, cancellation (in action, during delay, before start), invalid arguments |
| `UtilityHelper` | UTC now, JSON document formatting, proto "not set" timestamp, `RandomInt` range, `SetKind`, `GetMinWithKind` |
| `ServiceHelper.SetupMethod` | response with `Feedback.RequestJsonString`, null request error |
| AutoMapper profiles | `RequestDownloadRequest → Download`, `Download → DownloadRequestDto / AnalysisRequestDto`, `RequestExtractInfoFormatsRequest → ExtractInfo` |
| Hand-written mappers | `ConfigureVdMapper.Map` (download list, defaults for empty fields), `ClientVdMapper.Map` (formats, fragments, null formats) |

### DownloadUpdater

| Area | Covered |
| --- | --- |
| `ConfigureVdMapper.Map` | null DTO / null formats, defaults for empty fields, format values, fragments, downloader options, `HasDrm` → int |
| `DownloadUpdateDto` | deserialising the Python downloader's progress message, `activity` read as `dd/MM/yyyy` local time → UTC |

### Distributor

| Area | Covered |
| --- | --- |
| `IgnoreFile` | GUID-named finished files pass; `.part`, `.m4a`, `.ytdlp`, `.json`, multi-dot, non-GUID, BBC / Facebook fragments and listed formats are ignored; missing format list |
| `MoveFileAsync` | move to the requested folder and name and report distributed; unknown client id; missing destination folder (share file deleted, reported deleted); failed distributed update; locked file (retry and delete-failure logs) |
| `AppSettings` | `FormatTypeWhitelist` keeps only `true` entries; missing section gives an empty list |
| `Sanitise` | lower-casing and replacing unsafe characters |

### Ytdlp

| Area | Covered |
| --- | --- |
| `ParseVersion` | GitHub release span → `2025.01.15` (prefix and whitespace stripped); no prefix setting; changed markup / empty span / null page give `""` |
| `CheckVersionAsync` | posts `/v1/ytdlp/version/{version}`; no POST when no version is found or the page fails to load; Manager unreachable or returning 500 gives `false` without throwing |
| `AppSettings` | values read from configuration |

### Downloader (pytest)

| Area | Covered |
| --- | --- |
| `getValue` / `getProgressValue` | empty markers (`None`, `""`, `"None"`, `"N/A"`), real values kept, ANSI colour codes and whitespace stripped |
| `setGlobals` / `getYtdlpOptions` | request values; int defaults when the Manager's string values are null or empty; output template, retries, timeout |
| `loadAppSettings` | env strings converted (`IS_DEBUG_MODE=False` → `False`, port / retry / timeout → int); defaults without env |
| Stall detection | `hasDownloadStalled` timer and reset; `progressHooks` raises `DownloadCancelled` when stalled |
| `progressHooks` | DOWNLOADING update fields, FINISHED (eta `00:00`, `100.0%`, finished), ERRORED for unknown status |
| `on_message_received` | VERSION reply, routing to download / analyse / extract, unsupported type, empty body, ack |
| `analyse` / `extract` / `download` | with a fake `yt_dlp.YoutubeDL`: video details, live streams rejected, extract errors logged, formats sent, error code sent as text, cancelled download clears partial files |
| Helpers | `getDateDiff` (including whole days), `activity` date format, `getSoftwareVersion`, `printLog`, `serializeDatetime`, `clearCancelledFiles` |

The Downloader tests replace the RabbitMQ producer and yt-dlp with fakes.

The Distributor and Ytdlp tests replace the Manager's REST API with a fake `HttpMessageHandler`, and the Ytdlp tests feed
HTML instead of loading GitHub. Both services keep settings in static fields (and the Distributor tests capture console
output and use temporary folders), so parallel test runs are switched off in those projects. None of the tests need a
database, RabbitMQ or network.

## Run

Whole stack (from the repo root):

```sh
docker compose --env-file .env.dev -f docker-compose.Dev.yaml up --build -d
```

Manager only, locally:

- PostgreSQL: the Dev compose exposes it on `localhost:5433`. The connection string key is
  `ConnectionStrings:DownloadsDb` (database `MS.VdDownloader`).
- RabbitMQ on `localhost:5672` (`AppSettings:ChannelHostName` / `ChannelPortNumber`).
- `cd MS.Vid-dldr.Manager && dotnet run`. The project targets net7.0; if only a newer runtime is installed, set
  `DOTNET_ROLL_FORWARD=Major`.
- In `Development`, Swagger UI and gRPC reflection are enabled.

## Resilience

| What | Where | Setting |
| --- | --- | --- |
| Startup migration retry | Manager `Program.cs` → `Helpers/StartupRetry.cs` | `AppSettings:StartupDbRetryCount` attempts (default 10), 5 s apart; covers Postgres starting after the API in compose |
| DB transient-failure retry | Manager `AddDbContext` → Npgsql `EnableRetryOnFailure` | 5 retries, max 10 s delay |
| Outbound HTTP | Ytdlp / Distributor `AddHttpClient("ms-vid-dldr-httpClient").AddResilienceHandler(...)` | existing retry pipelines |

With `EnableRetryOnFailure`, any explicit transaction (`BeginTransaction`, `TransactionScope`) must run inside
`dbContext.Database.CreateExecutionStrategy().ExecuteAsync(...)`, or EF throws at runtime. There are none today.

## Docker

The Dockerfiles call `dotnet restore/publish MS.Vid-dldr.<Service>.csproj` explicitly, because each folder also contains a
`.sln` (a plain `dotnet publish` fails with MSB1011, and the Manager solution references the test project, which is
outside the build context).
