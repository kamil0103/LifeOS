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
    const date = offset => { const parts = Object.fromEntries(new Intl.DateTimeFormat('en-US', { timeZone: 'America/Los_Angeles', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(Date.now() + offset * 86400000)).map(p => [p.type,p.value])); return parts.year + '-' + parts.month + '-' + parts.day }
    const workBody = { company: 'Synthetic career company', title: 'Engineer', includeInResume: false, accomplishments: [{ action: 'Built a service', outcome: 'Reduced synthetic latency', metric: '20% measured result' }] }
    const work = await request(a, 'POST', '/experience/work', workBody)
    const legacy = await request(a, 'POST', '/experience/work', { company: 'Legacy company', title: 'Developer', bullets: 'Keep this original bullet' })
    const project = await request(a, 'POST', '/experience/projects', { name: 'Synthetic firmware', description: 'Overview', includeInResume: true, accomplishments: [{ action: 'Wrote firmware', outcome: 'Added checks', metric: '12 test cases' }] })
    const skill = await request(a, 'POST', '/skills', { name: 'C++ synthetic', learningStatus: 'learning' })
    const course = await request(a, 'POST', '/education/courses', { name: 'Synthetic algorithms' })
    const cert = await request(a, 'POST', '/skills/certificates', { name: 'Synthetic cloud certificate', expiry: date(10) })
    const foreign = await request(b, 'POST', '/experience/projects', { name: 'Foreign private project' })
    assert(!(await request(a, 'GET', '/skills/evidence-options')).some(x => x.id === foreign.id))
    await request(a, 'POST', '/skills/' + skill.id + '/evidence', { sourceType: 'project', sourceId: foreign.id }, 400)
    const repeated = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/skills/' + skill.id + '/evidence', { sourceType: 'work', sourceId: work.id, notes: 'Measured task evidence' })))
    assert.equal(new Set(repeated.map(x => x.id)).size, 1)
    for (const [sourceType,sourceId] of [['project',project.id],['course',course.id],['certificate',cert.id]]) await request(a, 'POST', '/skills/' + skill.id + '/evidence', { sourceType,sourceId,notes:'Synthetic evidence' })
    assert.equal((await request(a, 'GET', '/skills/' + skill.id + '/evidence')).length, 4)
    await request(b, 'GET', '/skills/' + skill.id + '/evidence', undefined, 404); await request(b, 'DELETE', '/skills/' + skill.id + '/evidence/' + repeated[0].id, undefined, 404)
    let resume = await request(a, 'GET', '/documents/resume-data'); assert.equal(resume.experience.length, 1); assert.equal(resume.experience[0].id, legacy.id); assert.equal(resume.experience[0].bullets, 'Keep this original bullet'); assert(resume.projects[0].description.includes('12 test cases')); assert(resume.skills.flatMap(g => g.skills).includes('C++ synthetic (learning)'))
    await request(a, 'PUT', '/experience/work/' + work.id, { ...workBody, includeInResume: true }); resume = await request(a, 'GET', '/documents/resume-data'); assert.equal(resume.experience.length, 2); assert(resume.experience.find(x => x.id === work.id).bullets.includes('20% measured result'))
    await request(a, 'PUT', '/experience/projects/' + project.id, { name: project.name, description: 'Updated overview', includeInResume: false })
    await request(a, 'PUT', '/portfolio/projects/' + project.id + '/details', { name: project.name, description: 'Portfolio curated overview', isPortfolioProject: true })
    const savedProject = (await request(a, 'GET', '/experience/projects')).find(x => x.id === project.id); assert.equal(savedProject.includeInResume, false); assert.equal(savedProject.accomplishments.length, 1); assert.equal((await request(a, 'GET', '/documents/resume-data')).projects.length, 0)
    await Promise.all(Array.from({ length: 6 }, () => request(a, 'GET', '/planning/today')))
    let notices = (await request(a, 'GET', '/notifications')).filter(n => n.sourceKey?.startsWith('certificate:')); assert.equal(notices.length, 1); assert(notices[0].title.includes('expiring')); assert.equal(notices[0].actionUrl, '/skills?tab=certificates')
    await request(a, 'DELETE', '/notifications/' + notices[0].id, undefined, 204); await request(a, 'GET', '/planning/today'); assert.equal((await request(a, 'GET', '/notifications')).filter(n => n.sourceKey?.startsWith('certificate:')).length, 0)
    await request(a, 'PUT', '/skills/certificates/' + cert.id, { name: cert.name, expiry: date(-1) }); await request(a, 'GET', '/planning/today'); notices = (await request(a, 'GET', '/notifications')).filter(n => n.sourceKey?.startsWith('certificate:')); assert.equal(notices.length, 1); assert(notices[0].title.includes('expired'))
    await request(a, 'PUT', '/notifications/preferences', { followUpsEnabled: true, calendarEnabled: true, codingReviewsEnabled: true, calendarLeadMinutes: 30, certificateExpiryEnabled: false }, 204); await request(a, 'GET', '/planning/today'); assert.equal((await request(a, 'GET', '/notifications')).filter(n => n.sourceKey?.startsWith('certificate:')).length, 0)
    await request(a, 'PUT', '/notifications/preferences', { followUpsEnabled: true, calendarEnabled: true, codingReviewsEnabled: true, calendarLeadMinutes: 30 }, 204); assert.equal((await request(a, 'GET', '/notifications/preferences')).certificateExpiryEnabled, false)
    await request(a, 'DELETE', '/experience/work/' + work.id, undefined, 204); const missing = (await request(a, 'GET', '/skills/' + skill.id + '/evidence')).find(x => x.id === repeated[0].id); assert.equal(missing.available, false); assert.equal(missing.notes, 'Measured task evidence')
    await request(a, 'DELETE', '/skills/' + skill.id, undefined, 204); assert.equal(sql(`SELECT COUNT(*) FROM "SkillEvidence" WHERE "SkillId"='${skill.id}'`), '0')
    console.log('PASS: owned evidence sources, concurrent dedupe, deleted-source handling, learning labels, structured accomplishments, resume inclusion, shared-project preservation and certificate reminder preferences/dismissal')
  } finally { for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`); console.log('Disposable evidence/career accounts removed') }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
