import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { createManagerClient } from './api/managerClient'
import { ManagerClientProvider } from './api/ManagerClientProvider'

// Empty VITE_MANAGER_URL = same origin; in development Vite proxies /v1 to the Manager (see vite.config.ts).
const client = createManagerClient(import.meta.env.VITE_MANAGER_URL ?? '')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ManagerClientProvider client={client}>
      <App />
    </ManagerClientProvider>
  </StrictMode>,
)
