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
    const jobs = await Promise.all(Array.from({ length: 6 }, (_, i) => request(a, 'POST', '/jobs', { title: 'Synthetic engineer', company: 'Career test', location: 'Irvine', url: 'https://example.invalid/jobs/?posting=1&utm_source=feed' + i, description: 'Synthetic posting' })))
    assert.equal(new Set(jobs.map(j => j.id)).size, 1)
    const job = jobs[0]
    const distinct = await request(a, 'POST', '/jobs', { title: job.title, company: job.company, location: job.location, url: 'https://example.invalid/jobs/?posting=2' }); assert.notEqual(distinct.id, job.id)
    const resume = await request(a, 'POST', '/resume-versions', { title: 'Synthetic embedded resume', template: 'harvard', data: {} })
    const apps = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/job-applications', { jobId: job.id, resumeVersionId: resume.id, notes: 'Original application' })))
    assert.equal(new Set(apps.map(x => x.id)).size, 1); const application = apps[0]
    assert.equal(application.resumeVersionId, resume.id)
    const again = await request(a, 'POST', '/job-applications', { jobId: job.id, notes: 'Retry should not overwrite' }); assert.equal(again.notes, 'Original application'); assert.equal(again.resumeVersionId, resume.id)
    await request(b, 'POST', '/job-applications', { jobId: job.id }, 400)
    await request(b, 'DELETE', '/jobs/' + job.id, undefined, 404)
    await request(a, 'DELETE', '/jobs/' + job.id, undefined, 400)
    await request(a, 'DELETE', '/resume-versions/' + resume.id, undefined, 400)
    const docId = crypto.randomUUID()
    sql(`INSERT INTO "Documents" ("Id","UserId","JobId","Type","Filename","StoragePath","GeneratedAt","CreatedAt","UpdatedAt") VALUES ('${docId}','${a.user.id}','${job.id}','cover_letter','synthetic-letter.pdf','synthetic-path',NOW(),NOW(),NOW())`)
    await request(a, 'PUT', '/planning/applications/' + application.id + '/details', { resumeVersionId: resume.id, coverLetterDocumentId: docId }, 204)
    await request(a, 'DELETE', '/documents/' + docId, undefined, 400)
    assert.equal(sql(`SELECT COUNT(*) FROM "Documents" WHERE "Id"='${docId}'`), '1')
    const note = await request(a, 'POST', '/companynotes', { companyName: 'Career test', notes: 'Research', rating: 3 })
    await request(a, 'PUT', '/companynotes/' + note.id, { companyName: 'Career test', notes: 'Updated research', rating: 4 })
    await request(a, 'POST', '/companynotes', { companyName: 'Career test', rating: 6 }, 400)
    await request(b, 'PUT', '/companynotes/' + note.id, { companyName: 'Career test', notes: 'Foreign edit', rating: 4 }, 404)
    await request(a, 'DELETE', '/job-applications/' + application.id, undefined, 204)
    await request(a, 'DELETE', '/resume-versions/' + resume.id, undefined, 204)
    await request(a, 'DELETE', '/jobs/' + job.id, undefined, 204)
    assert.equal(sql(`SELECT "JobId" IS NULL FROM "Documents" WHERE "Id"='${docId}'`), 't')
    console.log('PASS: concurrent duplicate job/application saves, distinct posting IDs, resume selection, original notes preservation, owned research editing and linked asset/job deletion protection')
  } finally { for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`); console.log('Disposable career accounts removed') }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
