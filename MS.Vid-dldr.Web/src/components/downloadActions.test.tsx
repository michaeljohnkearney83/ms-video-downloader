import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fakeClient, renderWithClient } from '../test/fakeClient'
import { actionsFor } from './downloadActions'
import { DownloadList } from './DownloadList'

describe('actionsFor', () => {
  it.each([
    ['QUEUED', ['pause', 'cancel']],
    ['DOWNLOADING', ['pause', 'cancel']],
    ['RETRY', ['pause', 'cancel']],
    ['PAUSED', ['resume', 'cancel']],
    ['FINISHED', []],
    ['CANCELLED', []],
    [undefined, []],
  ])('%s -> %j', (status, actions) => {
    expect(actionsFor(status)).toEqual(actions)
  })
})

describe('DownloadList actions', () => {
  afterEach(() => vi.restoreAllMocks())

  const rows = (status: string) => ({
    getDownloadsBySession: vi.fn().mockResolvedValue({
      downloads: [{ clientId: 'a', requestedDownloadName: 'clip', requestedUrl: 'https://example.com/v', status }],
    }),
  })

  it('pauses a running download and shows the new status', async () => {
    const row = (status: string) => ({ downloads: [{ clientId: 'a', requestedDownloadName: 'clip', status }] })
    const client = fakeClient({
      // The Manager reports PAUSED once the pause is applied.
      getDownloadsBySession: vi.fn().mockResolvedValueOnce(row('DOWNLOADING')).mockResolvedValue(row('PAUSED')),
      downloadAction: vi.fn().mockResolvedValue({ clientId: 'a', status: 'PAUSED' }),
    })
    renderWithClient(<DownloadList sessionId="s1" />, client)

    await userEvent.click(await screen.findByRole('button', { name: 'Pause clip' }))

    expect(client.downloadAction).toHaveBeenCalledWith('a', 'pause')
    expect(await screen.findByText('PAUSED')).toBeInTheDocument()
  })

  it('offers resume for a paused download', async () => {
    const client = fakeClient(rows('PAUSED'))
    renderWithClient(<DownloadList sessionId="s1" />, client)

    await userEvent.click(await screen.findByRole('button', { name: 'Resume clip' }))

    expect(client.downloadAction).toHaveBeenCalledWith('a', 'resume')
    expect(screen.queryByRole('button', { name: 'Pause clip' })).not.toBeInTheDocument()
  })

  it('asks before cancelling, and does nothing when declined', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const client = fakeClient(rows('DOWNLOADING'))
    renderWithClient(<DownloadList sessionId="s1" />, client)

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel clip' }))

    expect(confirm).toHaveBeenCalledWith('Cancel clip and delete its partial file?')
    expect(client.downloadAction).not.toHaveBeenCalled()
  })

  it('cancels when confirmed', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const client = fakeClient({ ...rows('QUEUED'), downloadAction: vi.fn().mockResolvedValue({ clientId: 'a', status: 'CANCELLED' }) })
    renderWithClient(<DownloadList sessionId="s1" />, client)

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel clip' }))

    expect(client.downloadAction).toHaveBeenCalledWith('a', 'cancel')
  })

  it("shows the Manager's reason when an action isn't allowed", async () => {
    const client = fakeClient({
      ...rows('DOWNLOADING'),
      downloadAction: vi.fn().mockRejectedValue(new Error('Only a queued or running download can be paused; this one is FINISHED')),
    })
    renderWithClient(<DownloadList sessionId="s1" />, client)

    await userEvent.click(await screen.findByRole('button', { name: 'Pause clip' }))

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('this one is FINISHED'))
  })

  it('shows no actions for a finished download', async () => {
    renderWithClient(<DownloadList sessionId="s1" />, fakeClient(rows('FINISHED')))

    await screen.findByText('clip')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
