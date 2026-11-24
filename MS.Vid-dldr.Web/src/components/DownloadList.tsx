import { useCallback, useState } from 'react'
import { useManagerClient } from '../api/managerClientContext'
import { describeError } from '../api/managerClient'
import type { DownloadProgress } from '../api/types'
import { usePolling } from '../hooks/usePolling'
import { Alert } from './Alert'
import { ProgressBar } from './ProgressBar'
import { StatusBadge } from './StatusBadge'

function formatDate(value: string | undefined) {
  if (!value) return '–'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

/** The session's downloads, refreshed from the Manager every few seconds. */
export function DownloadList({
  sessionId,
  initial = [],
  intervalMs = 3000,
  refreshKey = 0,
}: {
  sessionId: string
  initial?: DownloadProgress[]
  intervalMs?: number
  /** Change it to refresh straight away (e.g. after queueing a download). */
  refreshKey?: number
}) {
  const client = useManagerClient()
  const [downloads, setDownloads] = useState<DownloadProgress[]>(initial)
  const [error, setError] = useState('')
  const [updated, setUpdated] = useState<Date | null>(null)

  const load = useCallback(
    async (signal: { cancelled: boolean }) => {
      void refreshKey // a new refreshKey gives a new callback, which restarts polling and loads at once
      try {
        const response = await client.getDownloadsBySession(sessionId)
        if (signal.cancelled) return
        setDownloads(response.downloads ?? [])
        setError('')
        setUpdated(new Date())
      } catch (err) {
        if (!signal.cancelled) setError(describeError(err))
      }
    },
    [client, sessionId, refreshKey],
  )

  usePolling(load, intervalMs)

  return (
    <section className="card" aria-label="Downloads">
      <div className="card-header">
        <h2>Downloads</h2>
        <span className="hint">{updated ? `Updated ${updated.toLocaleTimeString()}` : 'Loading…'}</span>
      </div>
      {error && <Alert tone="error">{error}</Alert>}
      {downloads.length === 0 ? (
        <p className="hint">No downloads in this session yet.</p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
                <th>Progress</th>
                <th>Requested</th>
              </tr>
            </thead>
            <tbody>
              {downloads.map((download, index) => (
                <tr key={download.clientId ?? index}>
                  <td>
                    <div className="name">{download.requestedDownloadName || '(untitled)'}</div>
                    <a className="url" href={download.requestedUrl} target="_blank" rel="noreferrer">
                      {download.requestedUrl}
                    </a>
                    {download.feedback?.errorMessage && <div className="error-text">{download.feedback.errorMessage}</div>}
                  </td>
                  <td>
                    <StatusBadge status={download.status} />
                  </td>
                  <td>
                    <ProgressBar
                      value={download.progressPercentage}
                      label={`Progress of ${download.requestedDownloadName || download.requestedUrl || 'download'}`}
                    />
                  </td>
                  <td>{formatDate(download.requestedOn)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
