// Disposable PostgreSQL mission checks. Synthetic proposals avoid provider calls.
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const { execFileSync } = require('node:child_process')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api'
const users = []
async function req(path, token, body, method = body ? 'POST' : 'GET', expected = 200) {
  const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined })
  assert.equal(r.status, expected, path + ' returned ' + r.status)
  return expected === 204 ? null : r.json()
}
async function account() { const username = 'mission_test_' + crypto.randomBytes(6).toString('hex'); const a = await req('/auth/register', null, { username, email: username + '@example.invalid', password: crypto.randomBytes(20).toString('hex') }); assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return { token: a.accessToken, id: a.user.id } }
const quote = text => "'" + text.replace(/'/g, "''") + "'"
function sql(text) { execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-c', text], { stdio: 'ignore' }) }
async function seed(a) {
  const plan = await req('/planning/today', a.token)
  const p = { id: crypto.randomUUID(), date: plan.date + 'T00:00:00Z', timeZone: plan.timeZone, priorities: [{ id: crypto.randomUUID().replace(/-/g, ''), title: 'Synthetic practice', category: 'coding', priority: 'high', reason: 'Review' }, { id: crypto.randomUUID().replace(/-/g, ''), title: 'Synthetic reflection', category: 'personal', priority: 'low', reason: 'Reflect' }], aiSummary: 'Synthetic suggestion' }
  sql(`INSERT INTO "AiCoachMessages" ("Id","UserId","MessageType","Content","ContextJson","IsRead","CreatedAt","UpdatedAt") VALUES (${quote(p.id)},${quote(a.id)},'coach_mission_proposal',${quote(JSON.stringify(p))},${quote('mission-draft:' + plan.date)},false,NOW(),NOW())`)
  return p
}
;(async () => {
  try {
    const a = await account(), foreign = await account(), p = await seed(a)
    await req('/aicoach/mission', a.token, undefined, 'GET', 404)
    assert.equal((await req('/planning/today', a.token)).actions.filter(i => i.kind === 'mission').length, 0)
    assert.equal((await req('/aicoach/messages', a.token)).length, 0)
    const cached = await Promise.all([1, 2, 3].map(() => req('/aicoach/generate-mission', a.token, {}, 'POST')))
    assert.ok(cached.every(d => d.id === p.id))
    await req(`/aicoach/mission/proposal/${p.id}/apply`, foreign.token, { priorities: p.priorities }, 'POST', 404)
    const reviewed = { priorities: [...p.priorities].reverse().map(t => ({ ...t, title: 'Reviewed ' + t.title })), aiSummary: 'Reviewed summary' }
    const results = await Promise.all([1, 2, 3].map(() => req(`/aicoach/mission/proposal/${p.id}/apply`, a.token, reviewed)))
    assert.ok(results.every(m => m.id === results[0].id)); let m = results[0]
    assert.equal(m.priorities[0].id, p.priorities[1].id); assert.equal(m.aiSummary, reviewed.aiSummary)
    const stableId = m.priorities[0].id
    const changed = { priorities: [...m.priorities].reverse(), aiSummary: 'Edited summary', expectedUpdatedAt: m.updatedAt }
    const statuses = await Promise.all([1, 2].map(() => fetch(base + `/aicoach/mission/${m.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + a.token }, body: JSON.stringify(changed) }).then(r => r.status)))
    assert.deepEqual(statuses.sort(), [200, 409]); m = await req('/aicoach/mission', a.token)
    await req('/planning/complete', a.token, { actionId: `mission:${m.id}:${stableId}` }, 'POST', 204)
    let current = await req('/aicoach/mission', a.token)
    assert.equal(current.priorities.find(i => i.id === stableId).completed, true); assert.equal(current.priorities.find(i => i.id !== stableId).completed, false)
    await req(`/aicoach/mission/${m.id}`, a.token, { ...changed, expectedUpdatedAt: m.updatedAt }, 'PUT', 409)
    await req('/planning/complete', a.token, { actionId: `mission:${m.id}:0` }, 'POST', 409)
    await req(`/aicoach/mission/${m.id}`, a.token, { priorities: current.priorities.filter(i => !i.completed), expectedUpdatedAt: current.updatedAt }, 'PUT', 400)
    const replay = await req(`/aicoach/mission/proposal/${p.id}/apply`, a.token, reviewed); assert.equal(replay.aiSummary, 'Edited summary'); assert.equal(replay.priorities.find(i => i.id === stableId).completed, true)
    const f = await seed(foreign); await req(`/aicoach/mission/proposal/${f.id}`, foreign.token, undefined, 'DELETE', 204)
    await req(`/aicoach/mission/proposal/${f.id}/apply`, foreign.token, { priorities: f.priorities }, 'POST', 409)
    current = await req(`/aicoach/mission/${m.id}/complete`, a.token, {}, 'POST'); assert.equal(current.isCompleted, true); assert.ok(current.priorities.every(i => i.completed))
    console.log('PASS: draft-only suggestions, private cached proposals, concurrent apply/edit, explicit review, stable reordered completion, stale protection, completed-task preservation, discard and replay without overwrites')
  } finally { for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"=${quote(id)}`); console.log('Disposable mission accounts removed') }
})().catch(e => { console.error(e.message); process.exitCode = 1 })
