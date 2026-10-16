// PostgreSQL and API checks using disposable users. Existing Bible content is read only.
const { execFileSync } = require('node:child_process')
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api', users = []
const sql = query => execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-At', '-c', query], { encoding: 'utf8' }).trim()
async function request(account, method, path, body, expected = 200) {
  const response = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(account ? { Authorization: 'Bearer ' + account.accessToken } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  assert.equal(response.status, expected, method + ' ' + path + ' returned ' + response.status)
  const text = await response.text(); return text ? JSON.parse(text) : undefined
}
async function account() {
  const name = 'personal_test_' + crypto.randomBytes(6).toString('hex')
  const a = await request(null, 'POST', '/auth/register', { username: name, email: name + '@example.invalid', password: crypto.randomBytes(20).toString('hex') })
  assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return a
}
 ;(async () => {
  try {
    const a = await account(), b = await account()
    const today = new Intl.DateTimeFormat('en-US', { timeZone: 'America/Los_Angeles', weekday: 'short' }).format(new Date()), day = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'].indexOf(today)
    const habit = await request(a, 'POST', '/habits', { name: 'Synthetic numeric habit', category: 'Coding', targetValue: 10, unit: 'pages', scheduleDaysMask: 127 })
    const partial = await request(a, 'POST', '/habits/' + habit.id + '/progress', { value: 4 }); assert.equal(partial.isCompleted, false); assert.equal(partial.xpEarned, 0)
    assert.equal(sql(`SELECT "IsCompleted" FROM "HabitCompletions" WHERE "HabitId"='${habit.id}'`), 'f')
    assert.equal((await request(a, 'GET', '/dashboard/today')).habits[0].isCompleted, false)
    const report = await request(a, 'GET', '/analytics/report?days=7'); assert.equal(report.habitHeatmap.reduce((sum, day) => sum + day.count, 0), 0)
    const plan = await request(a, 'GET', '/planning/today'); const action = plan.actions.find(x => x.sourceId === habit.id); assert.equal(action.canComplete, false); assert.equal(action.completed, false)
    const done = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/habits/' + habit.id + '/progress', { value: 10 }))); assert.equal(done.reduce((sum, x) => sum + x.xpEarned, 0), 25); assert(done.every(x => x.isCompleted))
    assert.equal(sql(`SELECT COUNT(*) FROM "HabitCompletions" WHERE "HabitId"='${habit.id}'`), '1'); assert.equal(sql(`SELECT COUNT(*) FROM "XpTransactions" WHERE "SourceId"='${habit.id}'`), '1')
    await request(a, 'POST', '/habits/' + habit.id + '/progress', { value: 2 }, 409)
    await request(b, 'POST', '/habits/' + habit.id + '/progress', { value: 10 }, 404); await request(b, 'GET', '/habits/' + habit.id + '/completions', undefined, 404); await request(b, 'DELETE', '/habits/' + habit.id, undefined, 404)
    await request(a, 'DELETE', '/habits/' + habit.id, undefined, 204); assert.equal((await request(a, 'GET', '/habits/today')).length, 0); assert.equal((await request(a, 'GET', '/habits?archived=true')).length, 1); assert.equal((await request(a, 'GET', '/habits/' + habit.id + '/completions')).length, 1)
    await request(a, 'POST', '/habits/' + habit.id + '/restore', undefined, 204); assert.equal((await request(a, 'POST', '/habits/' + habit.id + '/progress', { value: 10 })).xpEarned, 0)
    const off = await request(a, 'POST', '/habits', { name: 'Synthetic off-day habit', category: 'Prayer', scheduleDaysMask: 1 << ((day + 1) % 7) }); assert(!(await request(a, 'GET', '/habits/today')).some(h => h.id === off.id)); assert(!(await request(a, 'GET', '/planning/today')).actions.some(x => x.sourceId === off.id)); await request(a, 'POST', '/habits/' + off.id + '/complete', undefined, 400)
    const edit = { name: 'Synthetic scheduled habit', category: 'Prayer', scheduleDaysMask: 1 << day, expectedUpdatedAt: off.updatedAt }
    await request(a, 'PUT', '/habits/' + off.id, edit); await request(a, 'PUT', '/habits/' + off.id, edit, 409)
    const statuses = await Promise.all(Array.from({ length: 6 }, () => fetch(base + '/habits/' + off.id + '/complete', { method: 'POST', headers: { Authorization: 'Bearer ' + a.accessToken } }).then(r => r.status)))
    assert.equal(statuses.filter(x => x === 200).length, 1); assert.equal(statuses.filter(x => x === 400).length, 5)
    assert.equal(sql(`SELECT COUNT(*) FROM "XpTransactions" WHERE "SourceId"='${off.id}'`), '1')
    await request(a, 'POST', '/habits', { name: 'Invalid schedule', scheduleDaysMask: 0 }, 400)
    console.log('PASS: PostgreSQL partial progress, exactly-once XP, concurrent retries, archive/restore/history, due-day filtering, stale edits and ownership')
  } finally { for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`); console.log('Disposable habit accounts removed') }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
