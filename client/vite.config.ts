import react from '@vitejs/plugin-react'
import { readFileSync } from 'node:fs'
import { defineConfig } from 'vitest/config'

const packageJson = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8')) as { version: string }

/**
 * The game server of the developer machine (server/README: `dotnet run --project src/Aetheris.Host`).
 * The Vite server forwards the API and the hubs to it, so the page and the API share one
 * origin and the session cookies stay first-party.
 */
const gameServer = process.env.AETHERIS_SERVER_URL ?? 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    __APP_VERSION__: JSON.stringify(packageJson.version),
  },
  server: {
    proxy: {
      '/api': { target: gameServer },
      '/hubs': { target: gameServer, ws: true },
      '/health': { target: gameServer },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    restoreMocks: true,
  },
})
