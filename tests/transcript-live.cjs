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
    const payload = { institution: { name: 'Synthetic Transcript School' }, degree: { name: 'BSc', field: 'Computing' }, courses: [{ name: 'Algorithms', code: 'CS 101', grade: 'A', credits: '3.5', term: 'Fall 2025' }] }
    const before = await request(a, 'POST', '/transcripts/preview', payload); assert.equal(before[0].status, 'new'); assert.equal((await request(a, 'GET', '/education/institutions')).length, 0)
    const results = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/transcripts/save', payload)))
    assert.equal(new Set(results.map(x => x.institutionId)).size, 1); assert.equal(new Set(results.map(x => x.degreeId)).size, 1); assert.equal(results.reduce((sum, x) => sum + x.coursesAdded, 0), 1)
    const after = await request(a, 'POST', '/transcripts/preview', payload); assert.equal(after[0].status, 'existing'); assert.equal(after[0].existingCourse.grade, 'A'); assert.equal((await request(b, 'POST', '/transcripts/preview', payload))[0].status, 'new')
    payload.institution.name = ' SYNTHETIC   transcript school '; payload.courses[0].code = 'cs101'; payload.courses[0].grade = 'B'
    assert.equal((await request(a, 'POST', '/transcripts/save', payload)).coursesSkipped, 1)
    let schools = await request(a, 'GET', '/education/institutions'); assert.equal(schools.length, 1); assert.equal(schools[0].degrees.length, 1); assert.equal(schools[0].degrees[0].courses.length, 1); assert.equal(schools[0].degrees[0].courses[0].grade, 'A')
    payload.courses[0].term = 'Spring 2026'; assert.equal((await request(a, 'POST', '/transcripts/save', payload)).coursesAdded, 1)
    assert.equal((await request(b, 'GET', '/education/institutions')).length, 0)
    payload.institution.name = 'Invalid school'; payload.courses.push({ name: 'Invalid credit', credits: 'unknown' }); await request(b, 'POST', '/transcripts/save', payload, 400); assert.equal((await request(b, 'GET', '/education/institutions')).length, 0)
    const approved = { institution: { name: 'Different extracted school' }, degree: { name: 'Different extracted degree' }, institutionId: results[0].institutionId, degreeId: results[0].degreeId, courses: [{ name: 'Algorithms', code: 'CS101', term: 'Fall 2025', grade: 'B', credits: '4' }] }
    let review = await request(a, 'POST', '/transcripts/preview', approved); assert.equal(review[0].status, 'existing')
    approved.courses[0] = { ...approved.courses[0], updateExisting: true, existingCourseId: review[0].existingCourse.id, expectedFingerprint: review[0].fingerprint }
    assert.equal((await request(a, 'POST', '/transcripts/save', approved)).coursesUpdated, 1)
    assert.equal((await request(a, 'POST', '/transcripts/save', approved)).coursesUpdated, 0)
    await request(b, 'POST', '/transcripts/preview', approved, 400)
    review = await request(a, 'POST', '/transcripts/preview', approved)
    await request(a, 'PUT', '/education/courses/' + review[0].existingCourse.id, { name: 'Algorithms', code: 'CS101', term: 'Fall 2025', grade: 'C', credits: 4, degreeId: approved.degreeId, institutionId: approved.institutionId, isMajorRelated: true })
    approved.courses[0].expectedFingerprint = review[0].fingerprint
    approved.courses.push({ name: 'Should roll back', code: 'INVALID_ADD', credits: '3' })
    await request(a, 'POST', '/transcripts/save', approved, 409)
    schools = await request(a, 'GET', '/education/institutions'); assert.equal(schools.length, 1); assert.equal(schools[0].degrees[0].courses.length, 2); assert.equal(schools[0].degrees[0].courses.find(c => c.term === 'Fall 2025').grade, 'C')
    const unassigned = { ...approved, degreeId: undefined, unassigned: true, courses: [{ name: 'Unassigned import', credits: '2' }] }
    assert.equal((await request(a, 'POST', '/transcripts/save', unassigned)).degreeId, null)
    console.log('PASS: concurrent transcript retries, normalized matching, preserved grades, distinct terms, ownership and invalid import rollback')
  } finally {
    for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`)
    console.log('Disposable transcript accounts removed')
  }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
