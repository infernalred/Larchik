import { existsSync, mkdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const projectRoot = path.resolve(__dirname, '..')
const certDir = path.resolve(projectRoot, 'certs')
const certPath = path.resolve(certDir, 'localhost.pem')
const keyPath = path.resolve(certDir, 'localhost-key.pem')

if (existsSync(certPath) && existsSync(keyPath)) {
  process.exit(0)
}

mkdirSync(certDir, { recursive: true })

const result = spawnSync(
  'openssl',
  [
    'req',
    '-x509',
    '-newkey',
    'rsa:2048',
    '-sha256',
    '-days',
    '3650',
    '-nodes',
    '-keyout',
    keyPath,
    '-out',
    certPath,
    '-subj',
    '/CN=localhost',
    '-addext',
    'subjectAltName=DNS:localhost,IP:127.0.0.1',
  ],
  { stdio: 'inherit' },
)

if (result.status !== 0) {
  console.error('Failed to generate HTTPS certificate for Vite dev server.')
  process.exit(result.status ?? 1)
}

console.log(`Generated HTTPS dev certificate:\n- ${certPath}\n- ${keyPath}`)
