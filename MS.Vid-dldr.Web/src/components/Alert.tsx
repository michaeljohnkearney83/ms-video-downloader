import type { ReactNode } from 'react'

export type AlertTone = 'info' | 'success' | 'warning' | 'error'

export function Alert({ tone = 'info', children }: { tone?: AlertTone; children: ReactNode }) {
  return (
    <div className={`alert alert-${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  )
}
