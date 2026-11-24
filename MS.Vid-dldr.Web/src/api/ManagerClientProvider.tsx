import type { ReactNode } from 'react'
import type { ManagerClient } from './managerClient'
import { ManagerClientContext } from './managerClientContext'

export function ManagerClientProvider({ client, children }: { client: ManagerClient; children: ReactNode }) {
  return <ManagerClientContext.Provider value={client}>{children}</ManagerClientContext.Provider>
}
