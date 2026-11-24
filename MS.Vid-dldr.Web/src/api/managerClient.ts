import type {
  ExtractFormatsRequest,
  ExtractFormatsResponse,
  GetDownloadsBySessionIdResponse,
  GetSessionIdResponse,
  HealthReport,
  IsServiceUpResponse,
  RequestDownloadRequest,
  RequestDownloadResponse,
  SessionKey,
} from './types'

export class ManagerApiError extends Error {
  readonly status: number
  /** W3C trace id of the failed call: search for it in Grafana (Tempo/Loki) or Sentry. */
  readonly traceId?: string

  constructor(message: string, status: number, traceId?: string) {
    super(message)
    this.name = 'ManagerApiError'
    this.status = status
    this.traceId = traceId
  }
}

/** Error text for the UI, with the trace id when there is one. */
export function describeError(error: unknown): string {
  const message = error instanceof Error ? error.message : String(error)
  return error instanceof ManagerApiError && error.traceId ? `${message} (trace ${error.traceId})` : message
}

function randomHex(bytes: number): string {
  const values = new Uint8Array(bytes)
  crypto.getRandomValues(values)
  return Array.from(values, (b) => b.toString(16).padStart(2, '0')).join('')
}

/**
 * Starts a trace in the browser: a W3C traceparent the Manager continues, so the whole download (Manager,
 * RabbitMQ, Downloader, DownloadUpdater) shares this trace id.
 */
export function newTraceContext(): { traceId: string; traceparent: string } {
  const traceId = randomHex(16)
  return { traceId, traceparent: `00-${traceId}-${randomHex(8)}-01` }
}

export interface ManagerClient {
  /** /health/ready: overall status and one entry per dependency. Throws only when the Manager can't be reached. */
  getHealth(): Promise<HealthReport>
  isServiceUp(callerName: string): Promise<boolean>
  getSession(key: SessionKey): Promise<GetSessionIdResponse>
  getDownloadsBySession(sessionId: string): Promise<GetDownloadsBySessionIdResponse>
  requestDownload(request: RequestDownloadRequest): Promise<RequestDownloadResponse>
  extractFormats(request: ExtractFormatsRequest): Promise<ExtractFormatsResponse>
}

const segment = (value: string) => encodeURIComponent(value.trim())

/**
 * Client for the ms-video-downloader Manager REST API.
 * @param baseUrl Manager origin, e.g. http://localhost:8000. Empty means same origin (the Vite dev proxy).
 */
export function createManagerClient(baseUrl = '', fetchImpl: typeof fetch = (...args) => fetch(...args)): ManagerClient {
  const root = baseUrl.replace(/\/+$/, '')

  async function send<T extends { feedback?: { errorMessage?: string } }>(path: string, init?: RequestInit): Promise<T> {
    const { traceId, traceparent } = newTraceContext()
    let response: Response
    try {
      response = await fetchImpl(root + path, {
        ...init,
        headers: {
          Accept: 'application/json',
          traceparent,
          ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        },
      })
    } catch (error) {
      throw new ManagerApiError(`Cannot reach the Manager API: ${(error as Error).message}`, 0, traceId)
    }

    const text = await response.text()
    let body: unknown = {}
    if (text) {
      try {
        body = JSON.parse(text)
      } catch {
        throw new ManagerApiError(`Unexpected response (HTTP ${response.status}): ${text.slice(0, 200)}`, response.status, traceId)
      }
    }

    if (!response.ok) {
      // gRPC JSON transcoding errors look like { code, message, details }.
      const message = (body as { message?: string }).message
      throw new ManagerApiError(message || `HTTP ${response.status}`, response.status, traceId)
    }

    const result = body as T
    if (result.feedback?.errorMessage) throw new ManagerApiError(result.feedback.errorMessage, response.status, traceId)
    return result
  }

  const post = <T extends { feedback?: { errorMessage?: string } }>(path: string, payload: unknown) =>
    send<T>(path, { method: 'POST', body: JSON.stringify(payload) })

  async function getHealth(): Promise<HealthReport> {
    const { traceId, traceparent } = newTraceContext()
    let response: Response
    try {
      response = await fetchImpl(`${root}/health/ready`, { headers: { Accept: 'application/json', traceparent } })
    } catch (error) {
      throw new ManagerApiError(`Cannot reach the Manager API: ${(error as Error).message}`, 0, traceId)
    }
    // 503 (Unhealthy) still carries the report.
    const body = (await response.json().catch(() => null)) as Partial<HealthReport> | null
    if (!body?.status || !Array.isArray(body.checks))
      throw new ManagerApiError(`Unexpected health response (HTTP ${response.status})`, response.status, traceId)
    return body as HealthReport
  }

  return {
    getHealth,
    async isServiceUp(callerName) {
      const result = await send<IsServiceUpResponse>(`/v1/generic/isServiceUp/callerName/${segment(callerName)}`)
      return result.isUp === 1
    },
    getSession: ({ username, fid, clientName, deploymentNo }) =>
      send<GetSessionIdResponse>(
        `/v1/client/session/${segment(username)}/${segment(fid)}/${segment(clientName)}/${segment(deploymentNo)}`,
      ),
    getDownloadsBySession: (sessionId) =>
      send<GetDownloadsBySessionIdResponse>(`/v1/client/downloads/${segment(sessionId)}`),
    requestDownload: (request) => post<RequestDownloadResponse>('/v1/client/download', request),
    extractFormats: (request) => post<ExtractFormatsResponse>('/v1/client/download/extractInfo/formats', request),
  }
}
