/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // The Manager's REST endpoint. Docker compose maps it to 8000; `dotnet run` uses http://localhost:5210.
  const managerUrl = env.MANAGER_URL || 'http://localhost:8000'

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/v1': { target: managerUrl, changeOrigin: true },
        '/health': { target: managerUrl, changeOrigin: true },
        // SignalR: negotiate over HTTP, then a WebSocket.
        '/hubs': { target: managerUrl, changeOrigin: true, ws: true },
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
    },
  }
})
