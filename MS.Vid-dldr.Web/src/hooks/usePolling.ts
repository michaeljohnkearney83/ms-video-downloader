import { useEffect } from 'react'

export interface PollSignal {
  cancelled: boolean
}

/**
 * Calls `callback` now and then every `intervalMs` while `enabled`, waiting for each call to finish first.
 * Wrap the callback in useCallback: a new callback restarts polling with an immediate call.
 */
export function usePolling(callback: (signal: PollSignal) => Promise<void> | void, intervalMs: number, enabled = true) {
  useEffect(() => {
    if (!enabled) return
    const signal: PollSignal = { cancelled: false }
    let timer: ReturnType<typeof setTimeout> | undefined

    const tick = async () => {
      try {
        await callback(signal)
      } finally {
        if (!signal.cancelled) timer = setTimeout(tick, intervalMs)
      }
    }
    void tick()

    return () => {
      signal.cancelled = true
      if (timer) clearTimeout(timer)
    }
  }, [callback, intervalMs, enabled])
}
