import type { DownloadProgress } from '../api/types'

/** The Manager clears the percentage once a download is complete; show those as full. */
const completeStatuses = new Set(['FINISHED', 'DISTRIBUTED_LOOKUP', 'DISTRIBUTED'])

export function progressFor(download: DownloadProgress): string | undefined {
  return download.status && completeStatuses.has(download.status) ? '100%' : download.progressPercentage
}
