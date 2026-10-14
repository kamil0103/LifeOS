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
  const unique = 'planning_test_' + crypto.randomBytes(6).toString('hex')
  const a = await request(null, 'POST', '/auth/register', { username: unique, email: unique + '@example.invalid', password: crypto.randomBytes(20).toString('hex') })
  assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return a
}
;(async () => {
  try {
    const a = await account(), b = await account()
    const habit = await request(a, 'POST', '/habits', { name: 'Planning test habit', category: 'health' })
    const job = await request(a, 'POST', '/jobs', { title: 'Test engineer', company: 'Planning test company', description: 'Synthetic test only' })
    const application = await request(a, 'POST', '/applications', { jobId: job.id })
    const due = new Date(Date.now() - 3600000).toISOString()
    await Promise.all(Array.from({ length: 6 }, () => request(a, 'PUT', `/planning/applications/${application.id}/follow-up`, { dueAt: due }, 204)))
    let events = await request(a, 'GET', '/calendar/events')
    const linked = events.filter(e => e.applicationId === application.id); assert.equal(linked.length, 1)
    await request(b, 'PUT', `/planning/applications/${application.id}/follow-up`, { dueAt: due }, 404)
    await request(b, 'DELETE', '/calendar/events/' + linked[0].id, undefined, 404)
    const plan = await request(a, 'GET', '/planning/today'); assert.equal(plan.actions[0].kind, 'followup'); assert(plan.actions.some(x => x.id === 'habit:' + habit.id))
    const foreignPlan = await request(b, 'GET', '/planning/today'); assert(!foreignPlan.actions.some(x => x.id === 'habit:' + habit.id))
    await request(a, 'GET', '/planning/today'); const notices = await request(a, 'GET', '/notifications')
    const reminder = notices.find(n => n.sourceKey?.startsWith('followup:')); assert(reminder); assert.equal(notices.filter(n => n.sourceKey === reminder.sourceKey).length, 1)
    await request(b, 'POST', '/notifications/' + reminder.id + '/snooze', { minutes: 60 }, 404)
    await request(a, 'POST', '/notifications/' + reminder.id + '/snooze', { minutes: 60 }, 204)
    assert(!(await request(a, 'GET', '/notifications')).some(n => n.id === reminder.id))
    await request(a, 'DELETE', '/notifications/' + reminder.id, undefined, 204)
    await request(a, 'GET', '/planning/today'); assert(!(await request(a, 'GET', '/notifications')).some(n => n.id === reminder.id))
    const start = new Date(Date.now() + 7200000).toISOString(), end = new Date(Date.now() + 9000000).toISOString()
    const scheduled = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/planning/schedule', { actionId: 'habit:' + habit.id, startTime: start, endTime: end, destination: 'local' })))
    assert.equal(new Set(scheduled.map(x => x.eventId)).size, 1); assert(scheduled.some(x => x.alreadyScheduled))
    await request(a, 'PUT', '/calendar/events/' + linked[0].id, { title: 'Moved follow-up', startTime: start, endTime: end, eventType: 'general' })
    const detail = await request(a, 'GET', '/applications/' + application.id); assert.equal(Date.parse(detail.followUpDate), Date.parse(start))
    await request(a, 'DELETE', '/calendar/events/' + linked[0].id, undefined, 204)
    assert.equal((await request(a, 'GET', '/applications/' + application.id)).followUpDate, null)
    await request(a, 'PUT', '/planning/applications/' + application.id + '/details', { recruiterName: 'Synthetic recruiter', recruiterEmail: 'recruiter@example.invalid' }, 204)
    assert.equal((await request(a, 'GET', '/applications/' + application.id)).recruiterName, 'Synthetic recruiter')
    const problem = await request(a, 'POST', '/coding/problems', { title: 'Synthetic review problem', difficulty: 'easy' })
    await request(a, 'POST', '/coding/problems/' + problem.id + '/solve', {})
    sql(`UPDATE "CodingProblems" SET "SolvedAt"=NOW()-INTERVAL '10 days' WHERE "Id"='${problem.id}'`)
    assert((await request(a, 'GET', '/planning/today')).actions.some(x => x.id === 'review:' + problem.id))
    const attemptsBefore = Number(sql(`SELECT COUNT(*) FROM "ProblemAttempts" WHERE "ProblemId"='${problem.id}'`))
    await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/planning/complete', { actionId: 'review:' + problem.id }, 204)))
    assert.equal(Number(sql(`SELECT COUNT(*) FROM "ProblemAttempts" WHERE "ProblemId"='${problem.id}'`)), attemptsBefore + 1)
    assert(!(await request(a, 'GET', '/planning/today')).actions.some(x => x.id === 'review:' + problem.id))
    const missionId = crypto.randomUUID()
    sql(`INSERT INTO "DailyMissions" ("Id","UserId","MissionDate","PrioritiesJson","CreatedAt","UpdatedAt") VALUES ('${missionId}','${a.user.id}',CURRENT_DATE,'[{"Title":"First","Priority":"high"},{"Title":"Second","Priority":"medium"}]',NOW(),NOW())`)
    await Promise.all([0, 1].map(i => request(a, 'POST', '/planning/complete', { actionId: 'mission:' + missionId + ':' + i }, 204)))
    assert.equal(sql(`SELECT "CompletedAt" IS NOT NULL FROM "DailyMissions" WHERE "Id"='${missionId}'`), 't')
    await request(b, 'POST', '/planning/complete', { actionId: 'mission:' + missionId + ':0' }, 404)
    await request(a, 'PUT', '/notifications/preferences', { followUpsEnabled: false, calendarEnabled: false, codingReviewsEnabled: false, calendarLeadMinutes: 30 }, 204)
    await request(a, 'GET', '/planning/today')
    console.log('PASS: live PostgreSQL ownership, concurrent follow-ups/scheduling/mission completion, calendar synchronization, reminder dedupe/snooze/dismiss and preferences')
  } finally {
    for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`)
    console.log('Disposable planning accounts removed')
  }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
