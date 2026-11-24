const toneByStatus: Record<string, string> = {
  REQUESTED: 'neutral',
  QUEUED: 'neutral',
  ANALYSIS: 'active',
  DOWNLOADING: 'active',
  FINISHED: 'success',
  DISTRIBUTED_LOOKUP: 'success',
  DISTRIBUTED: 'success',
  RETRY: 'warning',
  ERRORED: 'error',
  DELETED: 'muted',
  PAUSED: 'neutral',
  CANCELLED: 'muted',
  NONE: 'muted',
}

export function StatusBadge({ status }: { status: string | undefined }) {
  const value = status || 'NONE'
  return <span className={`badge badge-${toneByStatus[value] ?? 'neutral'}`}>{value.replace(/_/g, ' ')}</span>
}
