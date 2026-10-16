// Exercises owned company research with disposable PostgreSQL-backed accounts.
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const { execFileSync } = require('node:child_process')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api'
const users = []
async function request(path, token, body, method = body ? 'POST' : 'GET', status = 200) {
  const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined })
  assert.equal(r.status, status, path + ' returned ' + r.status)
  return status === 204 ? null : r.json()
}
async function account() {
  const username = 'research_test_' + crypto.randomBytes(6).toString('hex')
  const a = await request('/auth/register', null, { username, email: username + '@example.invalid', password: crypto.randomBytes(20).toString('hex') })
  assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return a.accessToken
}
;(async () => {
  try {
    const token = await account(), foreign = await account()
    let note = await request('/companynotes', token, { companyName: ' Research Devices ', notes: 'Legacy notes', rating: 4, website: 'https://example.com', industry: 'Electronics', products: 'Routers', technologyStack: 'C++', culture: 'Mentoring', interviewPreparation: 'Interrupts' })
    assert.equal(note.companyName, 'Research Devices')
    const job = await request('/jobs', token, { title: 'Firmware engineer', company: ' research DEVICES ', description: 'Synthetic role', source: 'manual', salaryRange: 'Synthetic range' })
    const app = await request('/job-applications', token, { jobId: job.id })
    await request('/jobs', foreign, { title: 'PRIVATE ROLE', company: 'Research Devices', description: 'Synthetic private role', source: 'manual' })
    await request('/jobs', token, { title: 'Unrelated suffix', company: 'Research Devices Labs', description: 'Synthetic role', source: 'manual' })
    const related = await request(`/companynotes/${note.id}/jobs`, token)
    assert.equal(related.length, 1); assert.equal(related[0].id, job.id); assert.equal(related[0].applicationId, app.id)
    await request(`/companynotes/${note.id}/jobs`, foreign, undefined, 'GET', 404)
    await request(`/companynotes/${note.id}`, foreign, { companyName: 'Foreign' }, 'PUT', 404)
    assert.equal((await request('/companynotes', foreign)).length, 0)
    note = await request(`/companynotes/${note.id}`, token, { companyName: note.companyName, notes: 'Old client update', rating: 2 }, 'PUT')
    assert.equal(note.technologyStack, 'C++'); assert.equal(note.interviewPreparation, 'Interrupts')
    const latest = (await request('/companynotes', token))[0]
    const payload = { companyName: note.companyName, expectedUpdatedAt: latest.updatedAt, products: 'One update' }
    const statuses = await Promise.all([1, 2].map(() => fetch(base + `/companynotes/${note.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token }, body: JSON.stringify(payload) }).then(r => r.status)))
    assert.deepEqual(statuses.sort(), [200, 409])
    await request(`/companynotes/${note.id}`, token, { companyName: note.companyName, website: 'javascript:alert(1)' }, 'PUT', 400)
    note = await request(`/companynotes/${note.id}`, token, { companyName: note.companyName, website: '', culture: '' }, 'PUT')
    assert.equal(note.website, null); assert.equal(note.culture, null); assert.equal(note.products, 'One update')
    await request(`/companynotes/${note.id}`, token, undefined, 'DELETE', 204)
    assert.ok((await request('/jobs', token)).some(j => j.id === job.id))
    console.log('PASS: structured research, old-client preservation, owned related jobs/applications, concurrent stale edits, validation and independent deletion')
  } finally {
    for (const id of users) execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-c', `DELETE FROM "Users" WHERE "Id"='${id}'`], { stdio: 'ignore' })
    console.log('Disposable company research accounts removed')
  }
})().catch(e => { console.error(e.message); process.exitCode = 1 })
