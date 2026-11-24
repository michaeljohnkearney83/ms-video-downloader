import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { fakeClient, renderWithClient } from '../test/fakeClient'
import { DownloadForm } from './DownloadForm'
import { DownloadList } from './DownloadList'
import { parsePercentage } from './parsePercentage'
import { ServiceStatus } from './ServiceStatus'
import { SessionForm } from './SessionForm'

describe('parsePercentage', () => {
  it.each([
    [' 42.5%', 42.5],
    ['100.0%', 100],
    ['null', null],
    ['', null],
    [undefined, null],
    ['120%', 100],
  ])('%s -> %s', (input, expected) => {
    expect(parsePercentage(input)).toBe(expected)
  })
})

describe('SessionForm', () => {
  it('gets a session id and remembers the key', async () => {
    const onSession = vi.fn()
    const { client } = renderWithClient(<SessionForm onSession={onSession} />)

    await userEvent.type(screen.getByLabelText('Username'), 'mjk')
    await userEvent.type(screen.getByLabelText('FID'), 'F100')
    await userEvent.click(screen.getByRole('button', { name: 'Start session' }))

    const key = { username: 'mjk', fid: 'F100', clientName: 'web', deploymentNo: '1' }
    expect(client.getSession).toHaveBeenCalledWith(key)
    expect(onSession).toHaveBeenCalledWith({ id: 'session-1', key })
    expect(JSON.parse(localStorage.getItem('ms-vid-dldr-web.session-key')!)).toEqual(key)
  })

  it('shows the Manager error', async () => {
    const client = fakeClient({ getSession: vi.fn().mockRejectedValue(new Error('Failed to get session id')) })
    renderWithClient(<SessionForm onSession={vi.fn()} />, client)

    await userEvent.type(screen.getByLabelText('Username'), 'mjk')
    await userEvent.type(screen.getByLabelText('FID'), 'F100')
    await userEvent.click(screen.getByRole('button', { name: 'Start session' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Failed to get session id')
  })
})

describe('DownloadForm', () => {
  it('queues a download for the session', async () => {
    const onQueued = vi.fn()
    const { client } = renderWithClient(<DownloadForm sessionId="s1" onQueued={onQueued} />)

    await userEvent.type(screen.getByLabelText('Video URL'), 'https://example.com/v')
    await userEvent.type(screen.getByLabelText('File name'), 'clip')
    await userEvent.click(screen.getByRole('button', { name: 'Download' }))

    expect(client.requestDownload).toHaveBeenCalledWith({
      downloadSessionId: 's1',
      requestedUrl: 'https://example.com/v',
      requestedDownloadFolder: '',
      requestedDownloadName: 'clip',
      requestedDownloadExt: 'mp4',
      requestedRetryCount: 3,
      requestedTimeoutSeconds: 600,
      requestedDownloadStalledTimeoutSeconds: 60,
      force: 0,
    })
    expect(await screen.findByText('Queued download client-1')).toBeInTheDocument()
    expect(onQueued).toHaveBeenCalledWith('client-1')
  })

  it('warns when the URL is already downloading', async () => {
    const client = fakeClient({ requestDownload: vi.fn().mockResolvedValue({ activeDownloadExists: 1 }) })
    renderWithClient(<DownloadForm sessionId="s1" />, client)

    await userEvent.type(screen.getByLabelText('Video URL'), 'https://example.com/v')
    await userEvent.click(screen.getByRole('button', { name: 'Download' }))

    expect(await screen.findByText(/already active/)).toBeInTheDocument()
  })

  it('lists formats and copies the chosen extension', async () => {
    const client = fakeClient({
      extractFormats: vi.fn().mockResolvedValue({
        extractInfo: { formats: [{ formatId: '251', ext: 'webm', acodec: 'opus', vcodec: 'none', filesize: 3_500_000 }] },
      }),
    })
    renderWithClient(<DownloadForm sessionId="s1" />, client)

    await userEvent.type(screen.getByLabelText('Video URL'), 'https://example.com/v')
    await userEvent.click(screen.getByRole('button', { name: 'Check formats' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Use .webm' }))

    expect(client.extractFormats).toHaveBeenCalledWith({ downloadSessionId: 's1', requestedUrl: 'https://example.com/v', timeoutMs: 0 })
    expect(screen.getByText('3.3 MB')).toBeInTheDocument()
    expect(screen.getByLabelText('Extension')).toHaveValue('webm')
  })
})

describe('DownloadList', () => {
  it('shows the session downloads with status and progress', async () => {
    const client = fakeClient({
      getDownloadsBySession: vi.fn().mockResolvedValue({
        downloads: [
          { clientId: 'a', requestedDownloadName: 'clip', requestedUrl: 'https://example.com/v', status: 'DOWNLOADING', progressPercentage: ' 42.5%' },
          { clientId: 'b', requestedUrl: 'https://example.com/w', status: 'ERRORED', progressPercentage: 'null', feedback: { errorMessage: 'HTTP 403' } },
        ],
      }),
    })
    renderWithClient(<DownloadList sessionId="s1" />, client)

    expect(await screen.findByText('clip')).toBeInTheDocument()
    expect(client.getDownloadsBySession).toHaveBeenCalledWith('s1')
    expect(screen.getByText('DOWNLOADING')).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'Progress of clip' })).toHaveAttribute('aria-valuenow', '42.5')
    expect(screen.getByText('HTTP 403')).toBeInTheDocument()
  })
})

describe('ServiceStatus', () => {
  it('shows when the Manager cannot be reached', async () => {
    const client = fakeClient({ getHealth: vi.fn().mockRejectedValue(new Error('Cannot reach the Manager API')) })
    renderWithClient(<ServiceStatus />, client)

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Manager unreachable'))
  })

  it('names each dependency and says why it is degraded', async () => {
    const client = fakeClient({
      getHealth: vi.fn().mockResolvedValue({
        status: 'Degraded',
        checks: [
          { name: 'database', status: 'Healthy', description: 'ok' },
          { name: 'masstransit-bus', status: 'Healthy' },
          { name: 'downloader', status: 'Degraded', description: 'No Downloader is consuming ms_vd_q_downloadRequest (2 message(s) waiting)' },
        ],
      }),
    })
    renderWithClient(<ServiceStatus />, client)

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Degraded'))
    const checks = screen.getByRole('list', { name: 'Health checks' })
    expect(checks).toHaveTextContent('Database')
    expect(checks).toHaveTextContent('Queue')
    expect(within(checks).getByText('Downloader', { exact: false }).closest('li')).toHaveTextContent('Downloader: Degraded')
    expect(screen.getByText(/No Downloader is consuming/)).toBeInTheDocument()
  })

  it('says all systems are up when healthy', async () => {
    const client = fakeClient({
      getHealth: vi.fn().mockResolvedValue({ status: 'Healthy', checks: [{ name: 'database', status: 'Healthy' }] }),
    })
    renderWithClient(<ServiceStatus />, client)

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('All systems up'))
    expect(screen.queryByText(/is Degraded/)).not.toBeInTheDocument()
  })
})
