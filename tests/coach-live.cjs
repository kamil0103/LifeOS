// Run on the LifeOS server with Node 18+ after deploying Phase 4.
// Uses disposable accounts only, then deletes their data in finally.
const { execFileSync } = require('node:child_process')
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const users = []
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api'
const sql = query => execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-At', '-c', query], { encoding: 'utf8' }).trim()
async function request(account, method, path, body, expected = 200) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(account ? { Authorization: 'Bearer ' + account.accessToken } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  assert.equal(res.status, expected, method + ' ' + path + ' returned ' + res.status)
  return text ? JSON.parse(text) : undefined
}
async function account() {
  const unique = 'coach_test_' + crypto.randomBytes(6).toString('hex')
  const a = await request(null, 'POST', '/auth/register', { username: unique, email: unique + '@example.invalid', password: crypto.randomBytes(20).toString('hex') })
  assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return a
}
;(async () => {
  try {
    const a = await account(), b = await account()
    const note = await request(a, 'POST', '/aicoach/memories', { content: 'My preferred practice language is Rust. COACH_MEMORY_MARKER' })
    const journalId = crypto.randomUUID()
    sql(`INSERT INTO "JournalEntries" ("Id","UserId","Title","Content","EntryDate","CreatedAt","UpdatedAt") VALUES ('${journalId}','${a.user.id}','Synthetic journal','COACH_JOURNAL_MARKER',NOW(),NOW(),NOW())`)
    let context = await request(a, 'GET', '/aicoach/context')
    assert(JSON.stringify(context).includes('COACH_MEMORY_MARKER')); assert(!JSON.stringify(context).includes('COACH_JOURNAL_MARKER'))
    assert(!(await request(b, 'GET', '/aicoach/memories')).some(m => m.id === note.id))
    await request(b, 'DELETE', '/aicoach/memories/' + note.id, undefined, 404)
    const prefs = { includeProfile: true, includeProgress: true, includeCalendar: false, includeJournal: true }
    await Promise.all(Array.from({ length: 3 }, () => request(a, 'PUT', '/aicoach/preferences', prefs, 204)))
    context = await request(a, 'GET', '/aicoach/context'); assert(JSON.stringify(context).includes('COACH_JOURNAL_MARKER'))
    await request(a, 'PUT', '/aicoach/preferences', { ...prefs, includeJournal: false }, 204)
    if (process.env.LIFEOS_COACH_SKIP_AI !== '1') {
    const requestId = crypto.randomUUID()
    const replies = await Promise.all(Array.from({ length: 3 }, () => request(a, 'POST', '/aicoach/chat', { requestId, message: 'For this exercise my practice board is ZX-426. Reply with that identifier only.' })))
    assert.equal(new Set(replies.map(r => r.response)).size, 1); assert.equal(replies.filter(r => !r.wasReplay).length, 1)
    assert.equal((await request(a, 'GET', '/aicoach/history')).length, 2)
    const followUp = await request(a, 'POST', '/aicoach/chat', { requestId: crypto.randomUUID(), message: 'What practice board did I mention in my previous message? Give only its identifier.' })
    assert(followUp.response.includes('ZX-426'), 'Follow-up should use the previous message')
    const memoryReply = await request(a, 'POST', '/aicoach/chat', { requestId: crypto.randomUUID(), message: 'What preferred practice language did I save as a note? Give only the language name.' })
    assert(/rust/i.test(memoryReply.response), 'Reply should use the explicit saved note')
    assert.equal((await request(b, 'GET', '/aicoach/history')).length, 0)
    assert(!JSON.stringify(await request(a, 'GET', '/aicoach/messages')).includes('COACH_MEMORY_MARKER'))
    const missions = await Promise.all(Array.from({ length: 2 }, () => request(a, 'POST', '/aicoach/generate-mission')))
    assert.equal(missions[0].id, missions[1].id); assert(missions[0].priorities.length >= 1 && missions[0].priorities.length <= 5)
    }
    await request(a, 'DELETE', '/aicoach/history', undefined, 204); assert.equal((await request(a, 'GET', '/aicoach/history')).length, 0)
    assert.equal((await request(a, 'GET', '/aicoach/memories')).length, 1)
    await request(a, 'DELETE', '/aicoach/memories/' + note.id, undefined, 204); assert(!JSON.stringify(await request(a, 'GET', '/aicoach/context')).includes('COACH_MEMORY_MARKER'))
    console.log(process.env.LIFEOS_COACH_SKIP_AI === '1' ? 'PASS: Coach preferences, journal opt-in, ownership and separate conversation/memory controls' : 'PASS: real AI conversation continuity and saved-note recall, concurrent replay/mission dedupe, journal opt-in, ownership and separate conversation/memory deletion')
  } finally { for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`); console.log('Disposable Coach accounts removed') }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
