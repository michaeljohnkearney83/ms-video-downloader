import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { vi } from 'vitest'
import type { ManagerClient } from '../api/managerClient'
import { ManagerClientProvider } from '../api/ManagerClientProvider'

export function fakeClient(overrides: Partial<ManagerClient> = {}): ManagerClient {
  return {
    getHealth: vi.fn().mockResolvedValue({ status: 'Healthy', checks: [] }),
    isServiceUp: vi.fn().mockResolvedValue(true),
    getSession: vi.fn().mockResolvedValue({ downloadSesssionId: 'session-1' }),
    getDownloadsBySession: vi.fn().mockResolvedValue({ downloads: [] }),
    requestDownload: vi.fn().mockResolvedValue({ clientId: 'client-1' }),
    extractFormats: vi.fn().mockResolvedValue({ extractInfo: { formats: [] } }),
    downloadAction: vi.fn().mockResolvedValue({ clientId: 'a', status: 'PAUSED' }),
    ...overrides,
  }
}

export function renderWithClient(ui: ReactElement, client: ManagerClient = fakeClient()) {
  return { client, ...render(<ManagerClientProvider client={client}>{ui}</ManagerClientProvider>) }
}
