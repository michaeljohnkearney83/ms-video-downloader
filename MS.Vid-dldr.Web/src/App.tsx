import { useState } from 'react'
import { DownloadForm } from './components/DownloadForm'
import { DownloadList } from './components/DownloadList'
import { ServiceStatus } from './components/ServiceStatus'
import { SessionForm, type Session } from './components/SessionForm'

export default function App() {
  const [session, setSession] = useState<Session | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

  return (
    <div className="app">
      <header className="app-header">
        <div>
          <h1>Video downloader</h1>
          <p className="hint">Front end for the ms-video-downloader Manager API</p>
        </div>
        <ServiceStatus />
      </header>

      <main>
        {session ? (
          <>
            <div className="session-bar">
              <span>
                Session <code>{session.id}</code> · {session.key.username} / {session.key.fid} ·{' '}
                {session.key.clientName} #{session.key.deploymentNo}
              </span>
              <button type="button" className="link" onClick={() => setSession(null)}>
                Change
              </button>
            </div>
            <DownloadForm sessionId={session.id} defaultFolder={session.defaultFolder} onQueued={() => setRefreshKey((key) => key + 1)} />
            <DownloadList sessionId={session.id} refreshKey={refreshKey} />
          </>
        ) : (
          <SessionForm onSession={setSession} />
        )}
      </main>
    </div>
  )
}
