import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const certDir = path.resolve(__dirname, 'certs')
const certPath = path.resolve(certDir, 'localhost.pem')
const keyPath = path.resolve(certDir, 'localhost-key.pem')
const httpsOptions =
  fs.existsSync(certPath) && fs.existsSync(keyPath)
    ? {
        cert: fs.readFileSync(certPath),
        key: fs.readFileSync(keyPath),
      }
    : undefined
const hmrConfig = {
  protocol: httpsOptions ? 'wss' : 'ws',
  host: 'localhost',
  port: 5173,
  clientPort: 5173,
}

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
    https: httpsOptions,
    hmr: hmrConfig,
  },
  preview: {
    host: 'localhost',
    port: 5173,
    strictPort: true,
    https: httpsOptions,
  },
})
