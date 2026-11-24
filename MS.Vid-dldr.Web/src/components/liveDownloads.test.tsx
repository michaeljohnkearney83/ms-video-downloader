import { act, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { LiveHandlers } from '../api/liveDownloads'
import { fakeClient, renderWithClient } from '../test/fakeClient'
import { DownloadList } from './DownloadList'
import { progressFor } from './progressFor'

/** A client whose live subscription the test drives by hand. */
function liveClient() {
  let handlers: LiveHandlers | undefined
  const unsubscribe = vi.fn()
  const client = fakeClient({
    getDownloadsBySession: vi.fn().mockResolvedValue({
      downloads: [{ clientId: 'a', requestedDownloadName: 'clip', status: 'DOWNLOADING', progressPercentage: ' 10.0%' }],
    }),
    subscribeLive: vi.fn((_sessionId: string, h: LiveHandlers) => {
      handlers = h
      return unsubscribe
    }),
  })
  return { client, unsubscribe, push: () => handlers! }
}

describe('DownloadList live progress', () => {
  it('moves the progress bar on a pushed tick, without asking the Manager again', async () => {
    const { client, push } = liveClient()
    renderWithClient(<DownloadList sessionId="s1" />, client)
    expect(await screen.findByRole('progressbar')).toHaveAttribute('aria-valuenow', '10')
    expect(client.subscribeLive).toHaveBeenCalledWith('s1', expect.anything())

    // Going live switches to the slow safety-net poll, which reads the list once straight away.
    act(() => push().onState('live'))
    await waitFor(() => expect(client.getDownloadsBySession).toHaveBeenCalledTimes(2))

    act(() => push().onProgress({ clientId: 'a', status: 'DOWNLOADING', percent: ' 42.5%' }))

    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '42.5')
    expect(screen.getByLabelText('Live')).toBeInTheDocument()
    expect(client.getDownloadsBySession).toHaveBeenCalledTimes(2)
  })

  it('re-reads the list when a download changes state', async () => {
    const { client, push } = liveClient()
    renderWithClient(<DownloadList sessionId="s1" />, client)
    await screen.findByRole('progressbar')

    act(() => push().onChanged({ clientId: 'a', status: 'FINISHED' }))

    await waitFor(() => expect(client.getDownloadsBySession).toHaveBeenCalledTimes(2))
  })

  it('stops listening when the list goes away', async () => {
    const { client, unsubscribe } = liveClient()
    const { unmount } = renderWithClient(<DownloadList sessionId="s1" />, client)
    await screen.findByRole('progressbar')

    unmount()

    expect(unsubscribe).toHaveBeenCalled()
  })

  it('without live support it just polls, and shows no live marker', async () => {
    renderWithClient(<DownloadList sessionId="s1" />, fakeClient())
    await screen.findByText('No downloads in this session yet.')
    expect(screen.queryByLabelText('Live')).not.toBeInTheDocument()
  })
})

describe('progressFor', () => {
  it.each([
    ['DOWNLOADING', ' 42.5%', ' 42.5%'],
    ['FINISHED', 'null', '100%'],
    ['DISTRIBUTED', undefined, '100%'],
    ['PAUSED', ' 23.0%', ' 23.0%'],
  ])('%s %s -> %s', (status, progressPercentage, expected) => {
    expect(progressFor({ status, progressPercentage })).toBe(expected)
  })
})
