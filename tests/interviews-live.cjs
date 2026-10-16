// Disposable PostgreSQL checks for interview/calendar atomicity and ownership.
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const { execFileSync } = require('node:child_process')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api'
const users = []
async function req(path, token, body, method = body ? 'POST' : 'GET', status = 200) {
  const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined })
  assert.equal(r.status, status, path + ' returned ' + r.status)
  return status === 204 ? null : r.json()
}
async function account() { const username = 'interview_test_' + crypto.randomBytes(6).toString('hex'); const a = await req('/auth/register', null, { username, email: username + '@example.invalid', password: crypto.randomBytes(20).toString('hex') }); assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return a.accessToken }
;(async () => {
  try {
    const token = await account(), foreign = await account()
    const job = await req('/jobs', token, { title: 'Firmware engineer', company: 'Interview Devices', description: 'Synthetic role', source: 'manual' })
    const app = await req('/job-applications', token, { jobId: job.id })
    const path = `/job-applications/${app.id}/interviews`
    const r = { requestId: crypto.randomUUID(), round: 'Technical', format: 'video', status: 'scheduled', startTime: '2026-11-02T10:00:00-08:00', endTime: '2026-11-02T11:00:00-08:00', meetingUrl: 'https://example.com/meeting', preparation: 'Interrupts' }
    const creates = await Promise.all([1, 2, 3].map(() => req(path, token, r)))
    let item = creates[0]; assert.ok(creates.every(i => i.id === item.id)); assert.equal(item.history.length, 1)
    let events = await req('/calendar/events', token); assert.equal(events.filter(e => e.applicationId === app.id).length, 1); assert.equal(events.find(e => e.id === item.calendarEventId).eventType, 'interview')
    await req(path, foreign, undefined, 'GET', 404); await req(path, foreign, r, 'POST', 404)
    await req(path + '/' + item.id, token, r, 'PUT', 400)
    await req('/calendar/events/' + item.calendarEventId, token, { title: 'Bypass', startTime: r.startTime, endTime: r.endTime }, 'PUT', 400)
    await req('/calendar/events/' + item.calendarEventId, token, undefined, 'DELETE', 400)
    await req(path, token, { ...r, requestId: crypto.randomUUID(), round: 'Overlapping' }, 'POST', 409)
    assert.equal((await req(path, token)).length, 1)
    const followUp = '2026-11-05T18:00:00Z'
    await req(`/planning/applications/${app.id}/follow-up`, token, { dueAt: followUp }, 'PUT', 204)
    const moved = { ...r, startTime: '2026-11-03T18:00:00Z', endTime: '2026-11-03T19:00:00Z', expectedUpdatedAt: item.updatedAt }
    const writes = await Promise.all([1, 2].map(() => fetch(base + path + '/' + item.id, { method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token }, body: JSON.stringify(moved) }).then(res => res.status)))
    assert.deepEqual(writes.sort(), [200, 409]); item = (await req(path, token))[0]; assert.equal(item.history.length, 2); assert.equal(Date.parse(item.history[0].startTime), Date.parse('2026-11-02T18:00:00Z'))
    const eventId = item.calendarEventId
    item = await req(path + '/' + item.id, token, { ...moved, expectedUpdatedAt: item.updatedAt, status: 'completed', outcome: 'Discussion recorded' }, 'PUT'); assert.equal(item.calendarEventId, eventId)
    item = await req(path + '/' + item.id, token, { ...moved, expectedUpdatedAt: item.updatedAt, status: 'cancelled', outcome: 'Cancelled for testing' }, 'PUT'); assert.equal(item.calendarEventId, null); assert.equal(item.history.length, 4)
    events = await req('/calendar/events', token); assert.equal(events.filter(e => e.applicationId === app.id).length, 1); assert.equal(events.find(e => e.applicationId === app.id).eventType, 'followup')
    const unchanged = (await req('/job-applications', token)).find(a => a.id === app.id); assert.equal(unchanged.status, 'applied'); assert.ok(unchanged.followUpDate.startsWith('2026-11-05T18:00:00'))
    await req(`/job-applications/${app.id}`, token, undefined, 'DELETE', 204)
    assert.equal((await req('/calendar/events', token)).filter(e => e.applicationId === app.id).length, 0)
    assert.ok((await req('/jobs', token)).some(j => j.id === job.id))
    console.log('PASS: concurrent retry-safe creation, ownership, local conflicts, revisioned reschedules, calendar guards, completed/cancelled history, separate follow-up/status and deletion')
  } finally {
    for (const id of users) execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-c', `DELETE FROM "Users" WHERE "Id"='${id}'`], { stdio: 'ignore' })
    console.log('Disposable interview accounts removed')
  }
})().catch(e => { console.error(e.message); process.exitCode = 1 })
