import type { DownloadAction } from '../api/types'

/** Waiting for, or being worked on by, the Downloader (same rule as the Manager's DownloadActions). */
const active = new Set(['REQUESTED', 'QUEUED', 'ANALYSIS', 'DOWNLOADING', 'RETRY'])

/** The actions the Manager allows for a download in this status, in button order. */
export function actionsFor(status: string | undefined): DownloadAction[] {
  if (!status) return []
  if (active.has(status)) return ['pause', 'cancel']
  if (status === 'PAUSED') return ['resume', 'cancel']
  return []
}

export const actionLabels: Record<DownloadAction, string> = {
  pause: 'Pause',
  resume: 'Resume',
  cancel: 'Cancel',
}
