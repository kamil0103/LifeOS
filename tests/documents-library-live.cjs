// Disposable PostgreSQL + stored PDF checks. No AI calls; cleans generated files.
const assert = require('node:assert/strict'), crypto = require('node:crypto')
const { execFileSync } = require('node:child_process')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api', accounts = []
async function req(path, token, body, method = body ? 'POST' : 'GET', expected = 200) { const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined }); assert.equal(r.status, expected, path + ' returned ' + r.status); return expected === 204 ? null : r.json() }
async function account() { const username = 'documents_test_' + crypto.randomBytes(6).toString('hex'); const a = await req('/auth/register', null, { username, email: username + '@example.invalid', password: crypto.randomBytes(20).toString('hex') }); assert.match(a.user.id, /^[0-9a-f-]{36}$/); const result = { token: a.accessToken, id: a.user.id }; accounts.push(result); return result }
;(async () => {
 try {
  const a = await account(), foreign = await account(), token = a.token
  const job = await req('/jobs', token, { title: 'Engineer', company: 'Synthetic Devices', description: 'Synthetic role', source: 'manual' })
  const app = await req('/job-applications', token, { jobId: job.id })
  const data = await req('/documents/resume-data', token); data.profile.fullName = 'Synthetic Candidate'; data.title = 'Source resume'; data.template = 'harvard'
  const source = await req('/resume-versions', token, { title: data.title, template: data.template, data })
  const foreignSource = await req('/resume-versions', foreign.token, { title: 'Private source', template: data.template, data })
  const generated = await fetch(base + '/documents/resume', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token }, body: JSON.stringify({ data, links: { applicationId: app.id, sourceResumeVersionId: source.id } }) }); assert.equal(generated.status, 200)
  let doc = (await req('/documents', token))[0]; assert.equal(doc.applicationId, app.id); assert.equal(doc.jobId, job.id); assert.equal(doc.sourceResumeVersionId, source.id); assert.equal(doc.sourceResumeVersionTitle, 'Source resume')
  await req(`/documents/${doc.id}/preview`, foreign.token, undefined, 'GET', 404)
  const preview = await fetch(base + `/documents/${doc.id}/preview`, { headers: { Authorization: 'Bearer ' + token } }); assert.equal(preview.status, 200); assert.ok(preview.headers.get('cache-control').includes('no-store')); assert.equal(preview.headers.get('x-content-type-options'), 'nosniff'); assert.equal(Buffer.from(await preview.arrayBuffer()).subarray(0,5).toString(), '%PDF-')
  assert.equal((await req('/documents', token)).length, 1); assert.equal((await req('/documents', foreign.token)).length, 0)
  await req(`/documents/${doc.id}/links`, foreign.token, { expectedUpdatedAt: doc.updatedAt }, 'PUT', 404)
  await req(`/documents/${doc.id}/links`, token, { expectedUpdatedAt: doc.updatedAt, sourceResumeVersionId: foreignSource.id }, 'PUT', 400)
  await req(`/resume-versions/${source.id}`, token, undefined, 'DELETE', 400)
  const body = { expectedUpdatedAt: doc.updatedAt, applicationId: app.id, sourceResumeVersionId: source.id }
  const statuses = await Promise.all([1, 2].map(() => fetch(base + `/documents/${doc.id}/links`, { method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token }, body: JSON.stringify(body) }).then(r => r.status))); assert.deepEqual(statuses.sort(), [200,409])
  const unchanged = (await req('/job-applications', token)).find(i => i.id === app.id); assert.equal(unchanged.resumeVersionId, null); assert.equal(unchanged.coverLetterDocumentId, null)
  await req(`/job-applications/${app.id}`, token, undefined, 'DELETE', 204); doc = (await req('/documents', token))[0]; assert.equal(doc.applicationId, null); assert.equal(doc.jobId, job.id)
  doc = await req(`/documents/${doc.id}/links`, token, { expectedUpdatedAt: doc.updatedAt, jobId: job.id }, 'PUT'); assert.equal(doc.sourceResumeVersionId, null)
  await req(`/resume-versions/${source.id}`, token, undefined, 'DELETE', 204)
  console.log('PASS: real owned PDF preview, source/application generation links, private targets, concurrent metadata edits, unchanged application selections, source deletion guard and application removal preserving PDF/job context')
 } finally {
  let cleanupError
  for (const a of accounts) {
   try { for (const doc of await req('/documents', a.token)) await req(`/documents/${doc.id}`, a.token, undefined, 'DELETE', 204) } catch (e) { cleanupError = e }
   execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-c', `DELETE FROM "Users" WHERE "Id"='${a.id}'`], { stdio: 'ignore' })
  }
  if (cleanupError) throw new Error('Database accounts removed but generated-file cleanup needs checking: ' + cleanupError.message)
  console.log('Disposable documents/files/accounts removed')
 }
})().catch(e => { console.error(e.message); process.exitCode = 1 })
