import { useState, type FormEvent } from 'react'
import { useManagerClient } from '../api/managerClientContext'
import { describeError } from '../api/managerClient'
import type { Format, RequestDownloadRequest } from '../api/types'
import { Alert, type AlertTone } from './Alert'
import { FormatsTable } from './FormatsTable'

const extensions = ['mp4', 'mkv', 'webm', 'mp3', 'm4a']

interface Message {
  tone: AlertTone
  text: string
}

/** Queues a download on the Manager, which sends it to the Downloader over RabbitMQ (MassTransit). */
export function DownloadForm({ sessionId, onQueued }: { sessionId: string; onQueued?: (clientId: string) => void }) {
  const client = useManagerClient()
  const [url, setUrl] = useState('')
  const [folder, setFolder] = useState('')
  const [name, setName] = useState('')
  const [ext, setExt] = useState('mp4')
  const [retryCount, setRetryCount] = useState(3)
  const [timeoutSeconds, setTimeoutSeconds] = useState(600)
  const [stalledSeconds, setStalledSeconds] = useState(60)
  const [force, setForce] = useState(false)
  const [busy, setBusy] = useState<'download' | 'formats' | null>(null)
  const [message, setMessage] = useState<Message | null>(null)
  const [formats, setFormats] = useState<Format[] | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy('download')
    setMessage(null)
    const request: RequestDownloadRequest = {
      downloadSessionId: sessionId,
      requestedUrl: url.trim(),
      requestedDownloadFolder: folder.trim(),
      requestedDownloadName: name.trim(),
      requestedDownloadExt: ext,
      requestedRetryCount: retryCount,
      requestedTimeoutSeconds: timeoutSeconds,
      requestedDownloadStalledTimeoutSeconds: stalledSeconds,
      force: force ? 1 : 0,
    }
    try {
      const response = await client.requestDownload(request)
      if (response.activeDownloadExists === 1) {
        setMessage({ tone: 'warning', text: 'A download for this URL is already active. Tick "Force" to queue another.' })
      } else {
        setMessage({ tone: 'success', text: `Queued download ${response.clientId ?? ''}`.trim() })
        if (response.clientId) onQueued?.(response.clientId)
      }
    } catch (error) {
      setMessage({ tone: 'error', text: describeError(error) })
    } finally {
      setBusy(null)
    }
  }

  async function checkFormats() {
    if (!url.trim()) {
      setMessage({ tone: 'warning', text: 'Enter a URL first.' })
      return
    }
    setBusy('formats')
    setMessage(null)
    setFormats(null)
    try {
      const response = await client.extractFormats({ downloadSessionId: sessionId, requestedUrl: url.trim(), timeoutMs: 0 })
      setFormats(response.extractInfo?.formats ?? [])
    } catch (error) {
      setMessage({ tone: 'error', text: describeError(error) })
    } finally {
      setBusy(null)
    }
  }

  return (
    <form className="card" onSubmit={submit} aria-label="Request download">
      <h2>Request a download</h2>
      <label>
        Video URL
        <input type="url" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://…" required />
      </label>
      <div className="grid-3">
        <label>
          Folder
          <input value={folder} onChange={(e) => setFolder(e.target.value)} placeholder="Manager default" />
        </label>
        <label>
          File name
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Video title" />
        </label>
        <label>
          Extension
          <select value={ext} onChange={(e) => setExt(e.target.value)}>
            {[...new Set([...extensions, ext])].map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </label>
      </div>
      <details>
        <summary>Advanced</summary>
        <div className="grid-3">
          <label>
            Retries
            <input type="number" min={0} value={retryCount} onChange={(e) => setRetryCount(Number(e.target.value))} />
          </label>
          <label>
            Timeout (s)
            <input type="number" min={0} value={timeoutSeconds} onChange={(e) => setTimeoutSeconds(Number(e.target.value))} />
          </label>
          <label>
            Stalled after (s)
            <input type="number" min={0} value={stalledSeconds} onChange={(e) => setStalledSeconds(Number(e.target.value))} />
          </label>
        </div>
        <label className="checkbox">
          <input type="checkbox" checked={force} onChange={(e) => setForce(e.target.checked)} />
          Force (queue even if this URL is already downloading)
        </label>
      </details>

      {message && <Alert tone={message.tone}>{message.text}</Alert>}

      <div className="actions">
        <button type="button" className="secondary" onClick={checkFormats} disabled={busy !== null}>
          {busy === 'formats' ? 'Checking…' : 'Check formats'}
        </button>
        <button type="submit" disabled={busy !== null}>
          {busy === 'download' ? 'Queueing…' : 'Download'}
        </button>
      </div>

      {formats && <FormatsTable formats={formats} onUseExtension={setExt} />}
    </form>
  )
}
