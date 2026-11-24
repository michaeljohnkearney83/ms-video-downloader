import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { LiveDownloadMessage, LiveState } from '../api/liveDownloads'
import { useManagerClient } from '../api/managerClientContext'

/**
 * Live pushes for a session, when the client supports them. Returns 'offline' when it doesn't (the caller keeps
 * polling either way; it just polls less often while live).
 */
export function useLiveDownloads(
  sessionId: string,
  onProgress: (message: LiveDownloadMessage) => void,
  onChanged: (message: LiveDownloadMessage) => void,
): LiveState {
  const client = useManagerClient()
  const [state, setState] = useState<LiveState>(client.subscribeLive ? 'connecting' : 'offline')
  // Latest callbacks without resubscribing on every render.
  const handlers = useRef({ onProgress, onChanged })
  useLayoutEffect(() => {
    handlers.current = { onProgress, onChanged }
  })

  useEffect(() => {
    if (!client.subscribeLive) return
    return client.subscribeLive(sessionId, {
      onProgress: (m) => handlers.current.onProgress(m),
      onChanged: (m) => handlers.current.onChanged(m),
      onState: setState,
    })
  }, [client, sessionId])

  return state
}
