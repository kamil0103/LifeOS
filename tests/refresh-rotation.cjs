// Run on the deployment host: node tests/refresh-rotation.cjs
// Creates and removes one synthetic account; requires access to lifeos-postgres.
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const { execFileSync } = require('node:child_process')
let token, userId
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api/'
async function request(path, data, method = 'POST') {
  const response = await fetch(base + path, {
    method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: data === undefined ? undefined : JSON.stringify(data), signal: AbortSignal.timeout(15000),
  })
  const text = await response.text()
  return { status: response.status, data: text ? JSON.parse(text) : null }
}
async function main() {
  try {
    const name = 'lifeos_test_' + crypto.randomBytes(6).toString('hex')
    const registration = await request('auth/register', { username: name, email: name + '@example.invalid', password: crypto.randomBytes(24).toString('base64url') })
    assert.equal(registration.status, 200)
    userId = registration.data.user.id
    token = registration.data.accessToken
    const original = registration.data.refreshToken
    const rotated = await request('auth/refresh', { refreshToken: original })
    assert.equal(rotated.status, 200)
    assert.notEqual(rotated.data.refreshToken, original)
    assert.equal((await request('auth/refresh', { refreshToken: original })).status, 401)
    console.log('PASS: original token rejected after rotation')
    const current = rotated.data.refreshToken
    const concurrent = await Promise.all([request('auth/refresh', { refreshToken: current }), request('auth/refresh', { refreshToken: current })])
    assert.deepEqual(concurrent.map(r => r.status).sort(), [200, 401])
    console.log('PASS: exactly one concurrent refresh succeeds')
    const winner = concurrent.find(r => r.status === 200).data
    token = winner.accessToken
    assert.equal((await request('auth/me', undefined, 'GET')).status, 200)
    assert.equal((await request('auth/logout', { refreshToken: winner.refreshToken })).status, 204)
    assert.equal((await request('auth/refresh', { refreshToken: winner.refreshToken })).status, 401)
    console.log('PASS: replacement token works and logout prevents refresh')
  } finally {
    if (userId) {
      assert.match(userId, /^[a-f0-9-]{36}$/)
      const sql = `DELETE FROM "Users" WHERE "Id" = '${userId}';`
      execFileSync('docker', ['exec', 'lifeos-postgres', 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -c "$1"', 'sh', sql], { stdio: 'pipe' })
      console.log('PASS: synthetic account removed')
    }
  }
}
main().catch(e => { console.error(e); process.exitCode = 1 })
