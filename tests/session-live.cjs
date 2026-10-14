// Checks session restoration and refresh rotation using a disposable account.
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const { execFileSync } = require('node:child_process')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api'
async function request(path, token, body) {
  const r = await fetch(base + path, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined })
  assert.equal(r.status, 200, path + ' returned ' + r.status)
  return r.json()
}
;(async () => {
  let id
  try {
    const name = 'session_test_' + crypto.randomBytes(6).toString('hex')
    let account = await request('/auth/register', null, { username: name, email: name + '@example.invalid', password: crypto.randomBytes(20).toString('hex') })
    id = account.user.id; assert.match(id, /^[0-9a-f-]{36}$/)
    for (let i = 0; i < 3; i++) {
      const restored = await request('/lifeos/session', account.accessToken)
      assert.equal(restored.user.id, id)
      account = await request('/auth/refresh', null, { refreshToken: account.refreshToken })
      assert.equal((await request('/lifeos/session', account.accessToken)).user.id, id)
    }
    console.log('PASS: session restored repeatedly before and after token rotation')
  } finally {
    if (id) { execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-c', `DELETE FROM "Users" WHERE "Id"='${id}'`], { stdio: 'ignore' }); console.log('Disposable session account removed') }
  }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
