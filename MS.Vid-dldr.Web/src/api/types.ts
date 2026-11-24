// Shapes of the Manager's REST API (gRPC JSON transcoding of Protos/ms_vdapi_calls_for_client.proto and
// ms_vdapi_calls_for_generic.proto). Field names are the proto names in lowerCamelCase. Proto3 JSON leaves out
// default values (empty strings, 0, empty lists), so every response field is optional.

export interface Feedback {
  informationMessage?: string
  warningMessage?: string
  errorMessage?: string
  exception?: string
  requestJsonString?: string
}

export interface SessionKey {
  username: string
  fid: string
  clientName: string
  deploymentNo: string
}

export interface DownloadProgress {
  clientId?: string
  requestedDownloadName?: string
  requestedUrl?: string
  /** RFC 3339 timestamp. */
  requestedOn?: string
  status?: DownloadStatus | string
  /** yt-dlp's text, e.g. " 42.5%", or "null" before progress starts. */
  progressPercentage?: string
  feedback?: Feedback
}

export interface GetSessionIdResponse {
  /** Spelled with three s's in the proto. */
  downloadSesssionId?: string
  downloads?: DownloadProgress[]
  feedback?: Feedback
  /** Where downloads go when the folder is left empty: HomeDrivePath + deployment no. */
  defaultDownloadFolder?: string
}

export interface GetDownloadsBySessionIdResponse {
  downloads?: DownloadProgress[]
  feedback?: Feedback
}

export interface RequestDownloadRequest {
  downloadSessionId: string
  requestedUrl: string
  requestedDownloadFolder: string
  requestedDownloadName: string
  requestedDownloadExt: string
  requestedRetryCount: number
  requestedTimeoutSeconds: number
  requestedDownloadStalledTimeoutSeconds: number
  /** 1 queues a new download even when one for the same URL is active. */
  force: 0 | 1
}

export interface RequestDownloadResponse {
  clientId?: string
  /** 1 when a download for this URL was already active and nothing was queued. */
  activeDownloadExists?: number
  feedback?: Feedback
}

export interface ExtractFormatsRequest {
  downloadSessionId: string
  requestedUrl: string
  /** 0 uses the Manager's ExtractInfoFormatTimeoutMs. */
  timeoutMs: number
}

export interface Format {
  formatId?: string
  formatNote?: string
  ext?: string
  protocol?: string
  acodec?: string
  vcodec?: string
  width?: number
  height?: number
  fps?: number
  tbr?: number
  resolution?: string
  filesize?: number
  filesizeApprox?: number
  language?: string
  dynamicRange?: string
}

export interface ExtractFormatsResponse {
  extractInfo?: { formats?: Format[] }
  activeExtractInfoExisted?: number
  feedback?: Feedback
}

export interface IsServiceUpResponse {
  isUp?: number
  feedback?: Feedback
}

/** pause: stop and keep the partial file; resume: continue it; cancel: stop and delete it. */
export type DownloadAction = 'pause' | 'resume' | 'cancel'

export interface DownloadActionResponse {
  clientId?: string
  /** The status after the action (PAUSED, QUEUED, CANCELLED), or the current one when it wasn't allowed. */
  status?: string
  feedback?: Feedback
}

/** The Manager's /health/ready report (ASP.NET Core health checks). HTTP 503 when Unhealthy, with the same body. */
export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface HealthCheck {
  /** database, masstransit-bus, downloader, updater */
  name: string
  status: HealthStatus
  description?: string | null
  durationMs?: number
  error?: string | null
  data?: Record<string, unknown> | null
}

export interface HealthReport {
  status: HealthStatus
  totalDurationMs?: number
  checks: HealthCheck[]
}

export const DownloadStatuses = [
  'NONE',
  'REQUESTED',
  'QUEUED',
  'ANALYSIS',
  'DOWNLOADING',
  'FINISHED',
  'DISTRIBUTED_LOOKUP',
  'DISTRIBUTED',
  'DELETED',
  'ERRORED',
  'RETRY',
  'PAUSED',
  'CANCELLED',
] as const

export type DownloadStatus = (typeof DownloadStatuses)[number]
