import { createContext, useContext } from 'react'
import type { ManagerClient } from './managerClient'

export const ManagerClientContext = createContext<ManagerClient | null>(null)

export function useManagerClient(): ManagerClient {
  const client = useContext(ManagerClientContext)
  if (!client) throw new Error('useManagerClient must be used inside <ManagerClientProvider>')
  return client
}
