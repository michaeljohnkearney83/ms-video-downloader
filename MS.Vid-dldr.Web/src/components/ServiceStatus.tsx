import { useCallback, useState } from 'react'
import { useManagerClient } from '../api/managerClientContext'
import { describeError } from '../api/managerClient'
import type { HealthReport, HealthStatus } from '../api/types'
import { usePolling } from '../hooks/usePolling'

/** Names the Manager's /health/ready checks for people. Unknown checks keep their own name. */
const checkLabels: Record<string, string> = {
  database: 'Database',
  'masstransit-bus': 'Queue',
  downloader: 'Downloader',
  updater: 'Updater',
}

const icon: Record<HealthStatus, string> = { Healthy: '✓', Degraded: '!', Unhealthy: '✕' }

const summary: Record<HealthStatus, string> = {
  Healthy: 'All systems up',
  Degraded: 'Degraded',
  Unhealthy: 'Down',
}

type State = { kind: 'checking' } | { kind: 'report'; report: HealthReport } | { kind: 'unreachable'; detail: string }

/**
 * Polls the Manager's /health/ready report: overall status plus one chip per dependency (database, queue, Downloader,
 * Updater), so a user can see why downloads aren't moving. Each chip has an icon and a label, not just a colour.
 */
export function ServiceStatus({ intervalMs = 15000 }: { intervalMs?: number }) {
  const client = useManagerClient()
  const [state, setState] = useState<State>({ kind: 'checking' })

  const check = useCallback(
    async (signal: { cancelled: boolean }) => {
      try {
        const report = await client.getHealth()
        if (!signal.cancelled) setState({ kind: 'report', report })
      } catch (error) {
        if (!signal.cancelled) setState({ kind: 'unreachable', detail: describeError(error) })
      }
    },
    [client],
  )

  usePolling(check, intervalMs)

  if (state.kind === 'checking')
    return (
      <div className="service-status service-checking" role="status">
        <span className="dot" aria-hidden="true" />
        Checking Manager…
      </div>
    )

  if (state.kind === 'unreachable')
    return (
      <div className="service-status service-unhealthy" role="status" title={state.detail}>
        <span className="dot" aria-hidden="true" />
        Manager unreachable
      </div>
    )

  const { report } = state
  return (
    <div className={`service-status service-${report.status.toLowerCase()}`} role="status">
      <span className="dot" aria-hidden="true" />
      <span className="service-summary">{summary[report.status]}</span>
      <ul className="health-checks" aria-label="Health checks">
        {report.checks.map((check) => (
          <li
            key={check.name}
            className={`health-check health-${check.status.toLowerCase()}`}
            title={check.description || check.error || check.status}
          >
            <span aria-hidden="true">{icon[check.status]}</span> {checkLabels[check.name] ?? check.name}
            <span className="visually-hidden">: {check.status}</span>
          </li>
        ))}
      </ul>
      {report.status !== 'Healthy' && (
        <p className="health-detail">
          {report.checks
            .filter((c) => c.status !== 'Healthy')
            .map((c) => c.description || c.error || `${checkLabels[c.name] ?? c.name} is ${c.status}`)
            .join(' · ')}
        </p>
      )}
    </div>
  )
}
