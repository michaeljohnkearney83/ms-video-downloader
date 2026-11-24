import { useCallback, useState } from 'react'
import { useManagerClient } from '../api/managerClientContext'
import { describeError } from '../api/managerClient'
import type { DownloadAction, DownloadProgress } from '../api/types'
import { usePolling } from '../hooks/usePolling'
import { useLiveDownloads } from '../hooks/useLiveDownloads'
import type { LiveDownloadMessage } from '../api/liveDownloads'
import { Alert } from './Alert'
import { ProgressBar } from './ProgressBar'
import { StatusBadge } from './StatusBadge'
import { actionLabels, actionsFor } from './downloadActions'
import { progressFor } from './progressFor'
import { loadHidden, saveHidden } from './hiddenDownloads'

function formatDate(value: string | undefined) {
  if (!value) return '–'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

/**
 * The session's downloads. Progress is pushed live (SignalR) when the Manager offers it; the list is still polled,
 * every `intervalMs` when offline and every `liveIntervalMs` as a safety net while live.
 */
export function DownloadList({
  sessionId,
  initial = [],
  intervalMs = 3000,
  liveIntervalMs = 30000,
  refreshKey = 0,
}: {
  sessionId: string
  initial?: DownloadProgress[]
  intervalMs?: number
  liveIntervalMs?: number
  /** Change it to refresh straight away (e.g. after queueing a download). */
  refreshKey?: number
}) {
  const client = useManagerClient()
  const [downloads, setDownloads] = useState<DownloadProgress[]>(initial)
  const [error, setError] = useState('')
  const [updated, setUpdated] = useState<Date | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [hidden, setHidden] = useState<Set<string>>(() => loadHidden(sessionId))

  const visible = downloads.filter((d) => !d.clientId || !hidden.has(d.clientId))
  const distributed = visible.filter((d) => d.status === 'DISTRIBUTED' && d.clientId)

  function clearDistributed() {
    const next = new Set(hidden)
    for (const d of distributed) next.add(d.clientId!)
    saveHidden(sessionId, next)
    setHidden(next)
  }

  async function act(download: DownloadProgress, action: DownloadAction) {
    if (!download.clientId) return
    const name = download.requestedDownloadName || download.requestedUrl || 'this download'
    if (action === 'cancel' && !window.confirm(`Cancel ${name} and delete its partial file?`)) return
    setBusy(download.clientId)
    try {
      const response = await client.downloadAction(download.clientId, action)
      // Show the new status straight away, then reload.
      setDownloads((rows) => rows.map((r) => (r.clientId === download.clientId ? { ...r, status: response.status ?? r.status } : r)))
      setError('')
      setReload((n) => n + 1)
    } catch (err) {
      setError(describeError(err))
    } finally {
      setBusy(null)
    }
  }

  const load = useCallback(
    async (signal: { cancelled: boolean }) => {
      void refreshKey // a new refreshKey gives a new callback, which restarts polling and loads at once
      void reload
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
    [client, sessionId, refreshKey, reload],
  )

  const onProgress = useCallback((message: LiveDownloadMessage) => {
    setDownloads((rows) =>
      rows.map((r) =>
        r.clientId === message.clientId
          ? { ...r, status: message.status, progressPercentage: message.percent || r.progressPercentage }
          : r,
      ),
    )
    setUpdated(new Date())
  }, [])
  const onChanged = useCallback(() => setReload((n) => n + 1), [])
  const live = useLiveDownloads(sessionId, onProgress, onChanged)

  usePolling(load, live === 'live' ? liveIntervalMs : intervalMs)

  return (
    <section className="card" aria-label="Downloads">
      <div className="card-header">
        <h2>Downloads</h2>
        {distributed.length > 0 && (
          <button
            type="button"
            className="secondary"
            onClick={clearDistributed}
            title="Hides delivered downloads from this list (they stay in the Manager)"
          >
            Clear distributed ({distributed.length})
          </button>
        )}
        <span className="hint">
          {live === 'live' && <span className="live-dot" title="Progress is pushed live from the Manager" aria-label="Live" />}
          {updated ? `Updated ${updated.toLocaleTimeString()}` : 'Loading…'}
        </span>
      </div>
      {error && <Alert tone="error">{error}</Alert>}
      {visible.length === 0 ? (
        <p className="hint">
          {downloads.length === 0 ? 'No downloads in this session yet.' : 'Nothing to show: delivered downloads were cleared.'}
        </p>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
                <th>Progress</th>
                <th>Requested</th>
                <th>
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((download, index) => (
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
                      value={progressFor(download)}
                      label={`Progress of ${download.requestedDownloadName || download.requestedUrl || 'download'}`}
                    />
                  </td>
                  <td>{formatDate(download.requestedOn)}</td>
                  <td>
                    <div className="row-actions">
                      {actionsFor(download.status).map((action) => (
                        <button
                          key={action}
                          type="button"
                          className={action === 'cancel' ? 'secondary danger' : 'secondary'}
                          disabled={busy === download.clientId}
                          onClick={() => act(download, action)}
                          aria-label={`${actionLabels[action]} ${download.requestedDownloadName || download.requestedUrl || 'download'}`}
                        >
                          {actionLabels[action]}
                        </button>
                      ))}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
