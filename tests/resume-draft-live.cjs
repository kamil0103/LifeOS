// Disposable PostgreSQL checks; PDF uses the configured renderer, no AI calls.
const assert = require('node:assert/strict'), crypto = require('node:crypto')
const { execFileSync } = require('node:child_process')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api', users = []
async function req(path, token, body, method = body ? 'POST' : 'GET', expected = 200) { const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined }); assert.equal(r.status, expected, path + ' returned ' + r.status); return expected === 204 ? null : r.json() }
async function account() { const username = 'resume_test_' + crypto.randomBytes(6).toString('hex'); const a = await req('/auth/register', null, { username, email: username + '@example.invalid', password: crypto.randomBytes(20).toString('hex') }); assert.match(a.user.id, /^[0-9a-f-]{36}$/); users.push(a.user.id); return a.accessToken }
;(async () => {
 try {
  const token = await account(), foreign = await account()
  await req('/resume-draft', token, undefined, 'GET', 404)
  const data = await req('/documents/resume-data', token); data.title = 'Synthetic draft'; data.template = 'harvard'; data.profile.fullName = 'Synthetic Candidate'; data.profile.summary = 'Synthetic resume preview'; data.sectionOrder = ['summary', 'skills']
  const writes = await Promise.all(['One', 'Two'].map(title => fetch(base + '/resume-draft', { method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token }, body: JSON.stringify({ data: { ...data, title } }) }).then(r => r.status)))
  assert.deepEqual(writes.sort(), [200, 409]); let draft = await req('/resume-draft', token)
  await req('/resume-draft', foreign, undefined, 'GET', 404)
  await req('/resume-draft', foreign, { expectedRevision: draft.revision, data }, 'PUT', 409)
  const oldRevision = draft.revision
  draft = await req('/resume-draft', token, { expectedRevision: draft.revision, data: { ...draft.data, title: 'Reviewed draft' } }, 'PUT')
  assert.notEqual(draft.revision, oldRevision)
  await req('/resume-draft', token, { expectedRevision: oldRevision, data }, 'PUT', 409)
  const replay = await req('/resume-draft', token, { data: draft.data }, 'PUT'); assert.equal(replay.revision, draft.revision)
  const pdf = await fetch(base + '/resume-draft/preview', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token }, body: JSON.stringify({ data: draft.data }) })
  assert.equal(pdf.status, 200); assert.ok(pdf.headers.get('content-type').startsWith('application/pdf')); assert.ok(pdf.headers.get('cache-control').includes('no-store')); const bytes = Buffer.from(await pdf.arrayBuffer()); assert.equal(bytes.subarray(0,4).toString(), '%PDF'); assert.ok(bytes.length > 500)
  assert.equal((await req('/documents', token)).length, 0); assert.equal((await req('/resume-versions', token)).length, 0)
  const version = await req('/resume-versions', token, { title: draft.data.title, template: draft.data.template, data: draft.data })
  await req('/resume-draft', token, { expectedRevision: draft.revision, data: { ...draft.data, title: 'Later draft' } }, 'PUT')
  assert.equal((await req('/resume-versions/' + version.id, token)).data.title, 'Reviewed draft')
  await req('/resume-versions/' + version.id, foreign, undefined, 'GET', 404)
  assert.equal((await req('/resume-versions', token)).length, 1)
  console.log('PASS: concurrent first saves, owned restoration, stale revisions, identical retry, real PDF preview without document/version writes, explicit named version preserved after autosave')
 } finally { for (const id of users) execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-c', `DELETE FROM "Users" WHERE "Id"='${id}'`], { stdio: 'ignore' }); console.log('Disposable resume accounts removed') }
})().catch(e => { console.error(e.message); process.exitCode = 1 })
