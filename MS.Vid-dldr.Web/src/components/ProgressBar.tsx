import { parsePercentage } from './parsePercentage'

export function ProgressBar({ value, label }: { value: string | undefined; label: string }) {
  const percent = parsePercentage(value)

  return (
    <div className="progress">
      <div
        className="progress-track"
        role="progressbar"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent ?? undefined}
      >
        <div className="progress-fill" style={{ width: `${percent ?? 0}%` }} />
      </div>
      <span className="progress-text">{percent === null ? '–' : `${percent.toFixed(1)}%`}</span>
    </div>
  )
}
