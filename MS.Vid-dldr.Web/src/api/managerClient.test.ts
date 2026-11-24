import { describe, expect, it, vi } from 'vitest'
import { createManagerClient, describeError, ManagerApiError } from './managerClient'

function fetchReturning(status: number, body: unknown) {
  return vi.fn().mockResolvedValue(new Response(typeof body === 'string' ? body : JSON.stringify(body), { status }))
}

describe('createManagerClient', () => {
  it('builds the session URL from encoded path segments', async () => {
    const fetchMock = fetchReturning(200, { downloadSesssionId: 'abc' })
    const client = createManagerClient('http://manager:8000/', fetchMock)

    const result = await client.getSession({ username: 'jo smith', fid: 'F/1', clientName: 'web', deploymentNo: '1' })

    expect(result.downloadSesssionId).toBe('abc')
    expect(fetchMock).toHaveBeenCalledWith('http://manager:8000/v1/client/session/jo%20smith/F%2F1/web/1', expect.anything())
  })

  it('posts a download request as JSON', async () => {
    const fetchMock = fetchReturning(200, { clientId: 'c1' })
    const client = createManagerClient('', fetchMock)
    const request = {
      downloadSessionId: 's1',
      requestedUrl: 'https://example.com/v',
      requestedDownloadFolder: '',
      requestedDownloadName: 'clip',
      requestedDownloadExt: 'mp4',
      requestedRetryCount: 3,
      requestedTimeoutSeconds: 600,
      requestedDownloadStalledTimeoutSeconds: 60,
      force: 0 as const,
    }

    await client.requestDownload(request)

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/v1/client/download')
    expect(init.method).toBe('POST')
    expect(init.headers['Content-Type']).toBe('application/json')
    expect(JSON.parse(init.body)).toEqual(request)
  })

  it('reads isUp as a boolean', async () => {
    expect(await createManagerClient('', fetchReturning(200, { isUp: 1 })).isServiceUp('web')).toBe(true)
    // Proto3 JSON leaves out zero values.
    expect(await createManagerClient('', fetchReturning(200, {})).isServiceUp('web')).toBe(false)
  })

  it('throws the feedback error message the Manager returns with HTTP 200', async () => {
    const client = createManagerClient('', fetchReturning(200, { feedback: { errorMessage: 'Failed to get session id' } }))

    await expect(client.getSession({ username: 'u', fid: 'f', clientName: 'c', deploymentNo: '1' })).rejects.toThrow(
      'Failed to get session id',
    )
  })

  it('throws the gRPC status message for an HTTP error', async () => {
    const client = createManagerClient('', fetchReturning(400, { code: 3, message: 'The filepath exceeds the max length 256' }))

    const error = await client.requestDownload({} as never).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ManagerApiError)
    expect((error as ManagerApiError).status).toBe(400)
    expect((error as ManagerApiError).message).toBe('The filepath exceeds the max length 256')
  })

  it('starts a W3C trace on every call', async () => {
    const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ downloads: [] })))

    await createManagerClient('', fetchMock).getDownloadsBySession('s1')
    await createManagerClient('', fetchMock).getDownloadsBySession('s1')

    const first = fetchMock.mock.calls[0][1].headers.traceparent as string
    const second = fetchMock.mock.calls[1][1].headers.traceparent as string
    expect(first).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/)
    expect(second.split('-')[1]).not.toBe(first.split('-')[1])
  })

  it('puts the trace id on errors so it can be looked up', async () => {
    const fetchMock = fetchReturning(400, { code: 3, message: 'bad' })

    const error = (await createManagerClient('', fetchMock).getDownloadsBySession('s1').catch((e: unknown) => e)) as ManagerApiError

    const sentTraceId = (fetchMock.mock.calls[0][1].headers.traceparent as string).split('-')[1]
    expect(error.traceId).toBe(sentTraceId)
    expect(describeError(error)).toBe(`bad (trace ${sentTraceId})`)
    expect(describeError(new Error('plain'))).toBe('plain')
  })

  it('reads the health report, including a 503 Unhealthy one', async () => {
    const report = { status: 'Unhealthy', checks: [{ name: 'database', status: 'Unhealthy', description: 'timeout' }] }
    const fetchMock = fetchReturning(503, report)

    const result = await createManagerClient('', fetchMock).getHealth()

    expect(result).toEqual(report)
    expect(fetchMock.mock.calls[0][0]).toBe('/health/ready')
  })

  it('throws when the health response is not a report', async () => {
    await expect(createManagerClient('', fetchReturning(502, '<html>Bad gateway</html>')).getHealth()).rejects.toThrow(
      'Unexpected health response (HTTP 502)',
    )
  })

  it('reports a network failure', async () => {
    const client = createManagerClient('', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(client.getDownloadsBySession('s1')).rejects.toThrow('Cannot reach the Manager API: Failed to fetch')
  })
})
