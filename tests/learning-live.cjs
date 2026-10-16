// Uses disposable users to verify real PostgreSQL locking, ownership and record relationships.
const { execFileSync } = require('node:child_process')
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api'
const users = []
const sql = query => execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-At', '-c', query], { encoding: 'utf8' }).trim()
async function request(account, method, path, body, expected = 200) {
  const res = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(account ? { Authorization: 'Bearer ' + account.accessToken } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  assert.equal(res.status, expected, method + ' ' + path + ' returned ' + res.status)
  const text = await res.text(); return text ? JSON.parse(text) : undefined
}
async function account() {
  const name = 'learning_test_' + crypto.randomBytes(6).toString('hex')
  const a = await request(null, 'POST', '/auth/register', { username: name, email: name + '@example.invalid', password: crypto.randomBytes(20).toString('hex') })
  assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return a
}
;(async () => {
  try {
    const a = await account(), b = await account()
    const p = await request(a, 'POST', '/coding/problems', { title: 'Synthetic learning test', difficulty: 'hard' })
    const input = { solutionLanguage: 'Rust', timeSpentMinutes: 20, notes: 'Ownership and O(n) complexity' }
    const solves = await Promise.all(Array.from({ length: 8 }, () => request(a, 'POST', `/coding/problems/${p.id}/solve`, input)))
    assert(solves.every(x => x.attemptCount === 1))
    let attempts = await request(a, 'GET', `/coding/problems/${p.id}/attempts`); assert.equal(attempts.length, 1); assert.equal(attempts[0].notes, input.notes); assert.equal(attempts[0].xpEarned, 55)
    assert.equal((await request(a, 'GET', '/coding/problems'))[0].attemptCount, 1)
    assert.equal(sql(`SELECT COUNT(*) FROM "XpTransactions" WHERE "UserId"='${a.user.id}' AND "Source"='coding'`), '1')
    await request(a, 'POST', `/coding/problems/${p.id}/unsolve`, {})
    assert.equal((await request(a, 'GET', '/coding/stats')).totalXpEarned, 55)
    await request(a, 'POST', `/coding/problems/${p.id}/solve`, input)
    await request(a, 'POST', `/coding/problems/${p.id}/reattempt`, { notes: 'Different same-day notes' }, 400)
    await request(b, 'GET', `/coding/problems/${p.id}/attempts`, undefined, 404)
    await request(b, 'POST', `/coding/problems/${p.id}/solve`, {}, 404)
    await request(a, 'POST', `/coding/problems/${p.id}/reattempt`, { timeSpentMinutes: 0 }, 400)
    // Move only this synthetic problem's timestamps to make a due review.
    sql(`UPDATE "ProblemAttempts" SET "SolvedAt"=NOW()-INTERVAL '10 days' WHERE "ProblemId"='${p.id}'; UPDATE "CodingProblems" SET "SolvedAt"=NOW()-INTERVAL '10 days' WHERE "Id"='${p.id}'`)
    await Promise.all([
      ...Array.from({ length: 4 }, () => request(a, 'POST', `/coding/problems/${p.id}/reattempt`, {})),
      ...Array.from({ length: 4 }, () => request(a, 'POST', '/planning/complete', { actionId: 'review:' + p.id }, 204)),
    ])
    attempts = await request(a, 'GET', `/coding/problems/${p.id}/attempts`); assert.equal(attempts.length, 2)
    assert(!(await request(a, 'GET', '/planning/today')).actions.some(x => x.id === 'review:' + p.id))
    const school = await request(a, 'POST', '/education/institutions', { name: 'Synthetic school A' })
    const school2 = await request(a, 'POST', '/education/institutions', { name: 'Synthetic school B' })
    await request(b, 'POST', '/education/degrees', { degreeName: 'Foreign parent', institutionId: school.id }, 400)
    const degree = await request(a, 'POST', '/education/degrees', { degreeName: 'Computer Science', institutionId: school.id })
    await request(b, 'POST', '/education/courses', { name: 'Foreign course', degreeId: degree.id }, 400)
    await request(a, 'POST', '/education/courses', { name: 'Mismatched course', degreeId: degree.id, institutionId: school2.id }, 400)
    const course = await request(a, 'POST', '/education/courses', { name: 'Algorithms', degreeId: degree.id, credits: 3 })
    assert.equal(course.institutionId, school.id)
    let schools = await request(a, 'GET', '/education/institutions'); assert.equal(schools.find(s => s.id === school.id).unassignedCourses.length, 0)
    await request(a, 'PUT', '/education/degrees/' + degree.id, { degreeName: 'Computer Science', institutionId: school2.id })
    assert.equal((await request(a, 'GET', '/education/courses'))[0].institutionId, school2.id)
    await request(a, 'DELETE', '/education/degrees/' + degree.id, undefined, 204)
    schools = await request(a, 'GET', '/education/institutions'); assert.equal(schools.find(s => s.id === school2.id).unassignedCourses[0].id, course.id)
    console.log('PASS: concurrent coding solves/reviews, XP deduplication, history, cross-user ownership and education reassignment/deletion')
  } finally {
    for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`)
    console.log('Disposable learning accounts removed')
  }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
