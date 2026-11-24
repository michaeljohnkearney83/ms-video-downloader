import { useState, type FormEvent } from 'react'
import { useManagerClient } from '../api/managerClientContext'
import { describeError } from '../api/managerClient'
import type { SessionKey } from '../api/types'
import { Alert } from './Alert'

const storageKey = 'ms-vid-dldr-web.session-key'

function loadSavedKey(): SessionKey {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey) ?? 'null') as Partial<SessionKey> | null
    if (saved) return { username: '', fid: '', clientName: 'web', deploymentNo: '1', ...saved }
  } catch {
    // ignore a corrupt entry
  }
  return { username: '', fid: '', clientName: 'web', deploymentNo: '1' }
}

export interface Session {
  id: string
  key: SessionKey
  /** HomeDrivePath + deployment no.: where downloads go when Folder is left empty. */
  defaultFolder?: string
}

/** Gets (or creates) a download session from the Manager for a user, FID, client and deployment. */
export function SessionForm({ onSession }: { onSession: (session: Session) => void }) {
  const client = useManagerClient()
  const [key, setKey] = useState<SessionKey>(loadSavedKey)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const update = (field: keyof SessionKey) => (event: { target: { value: string } }) =>
    setKey((current) => ({ ...current, [field]: event.target.value }))

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const response = await client.getSession(key)
      if (!response.downloadSesssionId) throw new Error('The Manager did not return a session id')
      localStorage.setItem(storageKey, JSON.stringify(key))
      onSession({ id: response.downloadSesssionId, key, defaultFolder: response.defaultDownloadFolder || undefined })
    } catch (err) {
      setError(describeError(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card" onSubmit={submit} aria-label="Start session">
      <h2>Start a session</h2>
      <p className="hint">
        The Manager groups downloads by user, FID, client name and deployment number. Use your <strong>Windows user
        name</strong>: the Distributor only moves finished files that belong to the user it runs as.
      </p>
      <div className="grid-2">
        <label>
          Username
          <input value={key.username} onChange={update('username')} required autoComplete="username" />
        </label>
        <label>
          FID
          <input value={key.fid} onChange={update('fid')} required />
        </label>
        <label>
          Client name
          <input value={key.clientName} onChange={update('clientName')} required />
        </label>
        <label>
          Deployment no.
          <input value={key.deploymentNo} onChange={update('deploymentNo')} required />
        </label>
      </div>
      {error && <Alert tone="error">{error}</Alert>}
      <div className="actions">
        <button type="submit" disabled={busy}>
          {busy ? 'Connecting…' : 'Start session'}
        </button>
      </div>
    </form>
  )
}
