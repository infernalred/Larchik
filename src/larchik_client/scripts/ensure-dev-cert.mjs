import { existsSync, mkdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const projectRoot = path.resolve(__dirname, '..')
const certDir = path.resolve(projectRoot, 'certs')
const certPath = path.resolve(certDir, 'localhost.pem')
const keyPath = path.resolve(certDir, 'localhost-key.pem')

mkdirSync(certDir, { recursive: true })

const hasCert = () => existsSync(certPath) && existsSync(keyPath)

const run = (command, args, stdio = 'inherit') =>
  spawnSync(command, args, { stdio })

const mkcertAvailable = run('mkcert', ['-help'], 'ignore').status === 0
const hasExistingCert = hasCert()

const isMkcertIssuedCertificate = () => {
  if (!hasExistingCert) return false

  const issuerResult = run('openssl', ['x509', '-in', certPath, '-noout', '-issuer'], 'pipe')
  if (issuerResult.status !== 0) return false

  const issuer = (issuerResult.stdout ?? '').toString()
  return issuer.toLowerCase().includes('mkcert development ca')
}

if (hasExistingCert && (!mkcertAvailable || isMkcertIssuedCertificate())) {
  process.exit(0)
}

if (mkcertAvailable) {
  const mkcertResult = run('mkcert', [
    '-cert-file',
    certPath,
    '-key-file',
    keyPath,
    'localhost',
    '127.0.0.1',
    '::1',
  ])

  if (mkcertResult.status !== 0) {
    console.warn('mkcert is available, but certificate generation failed. Falling back to openssl.')
  } else {
    console.log(`Generated HTTPS dev certificate (mkcert):\n- ${certPath}\n- ${keyPath}`)
    process.exit(0)
  }
}

if (hasExistingCert) {
  console.warn('Keeping existing certificate because mkcert generation did not succeed.')
  process.exit(0)
}

const opensslResult = run('openssl', [
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
])

if (opensslResult.status !== 0) {
  console.error('Failed to generate HTTPS certificate for Vite dev server.')
  process.exit(opensslResult.status ?? 1)
}

console.warn('Generated self-signed HTTPS certificate via openssl (not trusted by default).')
console.warn('Install mkcert to get trusted local certificates: https://github.com/FiloSottile/mkcert')
console.log(`Generated HTTPS dev certificate:\n- ${certPath}\n- ${keyPath}`)
