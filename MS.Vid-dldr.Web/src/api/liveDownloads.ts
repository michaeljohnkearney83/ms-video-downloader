import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'

/** A push from the Manager's /hubs/downloads (SignalR). */
export interface LiveDownloadMessage {
  clientId: string
  status: string
  /** yt-dlp's text, e.g. " 42.5%". */
  percent?: string | null
  speed?: string | null
  eta?: string | null
}

export type LiveState = 'connecting' | 'live' | 'offline'

export interface LiveHandlers {
  /** A DOWNLOADING tick: update the row in place. */
  onProgress(message: LiveDownloadMessage): void
  /** A state change (finished, errored, paused...): re-read the list. */
  onChanged(message: LiveDownloadMessage): void
  onState(state: LiveState): void
}

/** Stops the subscription. */
export type Unsubscribe = () => void

/**
 * Subscribes to a session's live progress. Reconnects by itself (and re-joins the session group); while it is
 * offline the list keeps polling, so nothing is lost, only slower.
 * @param baseUrl Manager origin; empty = same origin (nginx or the Vite proxy forward /hubs).
 */
export function subscribeLiveDownloads(baseUrl: string, sessionId: string, handlers: LiveHandlers): Unsubscribe {
  const connection = new HubConnectionBuilder()
    .withUrl(`${baseUrl.replace(/\/+$/, '')}/hubs/downloads`)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build()

  let stopped = false
  const join = () => connection.invoke('JoinSession', sessionId)

  connection.on('progress', handlers.onProgress)
  connection.on('changed', handlers.onChanged)
  connection.onreconnecting(() => handlers.onState('connecting'))
  connection.onreconnected(async () => {
    await join()
    handlers.onState('live')
    // Anything that happened while disconnected: catch up.
    handlers.onChanged({ clientId: '', status: 'RECONNECTED' })
  })
  connection.onclose(() => {
    if (!stopped) handlers.onState('offline')
  })

  // Automatic reconnect covers dropped connections, not a failed first start: retry that ourselves.
  let retry: ReturnType<typeof setTimeout> | undefined
  const start = async () => {
    handlers.onState('connecting')
    try {
      await connection.start()
      if (stopped) return
      await join()
      handlers.onState('live')
    } catch {
      if (stopped) return
      handlers.onState('offline')
      retry = setTimeout(start, 15000)
    }
  }
  void start()

  return () => {
    stopped = true
    if (retry) clearTimeout(retry)
    if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
  }
}
