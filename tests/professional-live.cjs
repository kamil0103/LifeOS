// Disposable PostgreSQL/API checks for professional records, including real public GitHub import.
const { execFileSync } = require('node:child_process')
const assert = require('node:assert/strict')
const crypto = require('node:crypto')
const base = process.env.LIFEOS_TEST_URL || 'http://127.0.0.1:8088/api'
const users = []
const sql = query => execFileSync('docker', ['exec', 'lifeos-postgres', 'psql', '-U', 'lifeos', '-d', 'lifeos', '-v', 'ON_ERROR_STOP=1', '-At', '-c', query], { encoding: 'utf8' }).trim()
async function request(account, method, path, body, expected = 200) {
  const response = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(account ? { Authorization: 'Bearer ' + account.accessToken } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  assert.equal(response.status, expected, method + ' ' + path + ' returned ' + response.status)
  const text = await response.text(); return text ? JSON.parse(text) : undefined
}
async function account() {
  const name = 'professional_test_' + crypto.randomBytes(6).toString('hex')
  const account = await request(null, 'POST', '/auth/register', { username: name, email: name + '@example.invalid', password: crypto.randomBytes(20).toString('hex') })
  assert.match(account.user.id, /^[0-9a-f-]{36}$/); users.push(account.user.id); return account
}
;(async () => {
  try {
    const a = await account(), b = await account()
    const skill = await request(a, 'POST', '/skills', { name: 'Rust', category: 'Programming Language', proficiency: 'Advanced', source: 'Deliberate choice' })
    const batch = [{ name: ' rust ', category: 'Tool', proficiency: 'Beginner' }, { name: 'C++', category: 'Programming Language', proficiency: 'Intermediate' }, { name: ' c++ ', category: 'Other', proficiency: 'Beginner' }]
    await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/skills/save-extracted', batch, 204)))
    const skills = await request(a, 'GET', '/skills'); assert.equal(skills.length, 2); assert.equal(skills.find(s => s.id === skill.id).proficiency, 'Advanced')
    await request(b, 'PUT', '/skills/' + skill.id, { name: 'Foreign edit' }, 404)
    await request(a, 'POST', '/skills/save-extracted', [{ name: 'Should not be saved', category: 'Tool', proficiency: 'Intermediate' }, { name: '', category: 'Tool', proficiency: 'Intermediate' }], 400)
    assert.equal((await request(a, 'GET', '/skills')).length, 2)
    const cert = await request(a, 'POST', '/skills/certificates', { name: 'Synthetic expired cert', dateObtained: '2024-01', expiry: '2025-12', url: 'https://example.invalid/verify' })
    assert.equal(cert.expiryStatus, 'expired'); await request(b, 'PUT', '/skills/certificates/' + cert.id, { name: 'Foreign' }, 404)
    await request(a, 'POST', '/skills/certificates', { name: 'Invalid', dateObtained: '2026-12', expiry: '2026-01' }, 400)
    const work = await request(a, 'POST', '/experience/work', { company: 'Synthetic company', title: 'Engineer', startDate: '2026-01', endDate: '2025', isCurrent: true, bullets: 'Test accomplishment' })
    assert.equal(work.endDate, null); await request(b, 'PUT', '/experience/work/' + work.id, { company: 'Foreign', title: 'Foreign' }, 404)
    await request(a, 'PUT', '/experience/work/' + work.id, { company: 'Synthetic company', title: 'Engineer', startDate: '2026-09', endDate: '2026-01' }, 400)
    assert.equal((await request(a, 'GET', '/experience/work'))[0].bullets, 'Test accomplishment')
    const project = await request(a, 'POST', '/experience/projects', { name: 'Curated LifeOS test project', description: 'Detailed custom impact', technologies: 'C++, C', link: 'https://github.com/kamil0103/LifeOS.git', isPortfolioProject: false })
    await request(a, 'PUT', '/portfolio/projects/' + project.id + '/details', { name: 'Must not persist', description: 'Must not persist', isPortfolioProject: true, deployedUrl: 'javascript:alert(1)' }, 400)
    assert.equal((await request(a, 'GET', '/experience/projects'))[0].name, project.name)
    await request(a, 'PUT', '/portfolio/projects/' + project.id + '/details', { ...project, isPortfolioProject: true, isFeatured: true, gitHubRepoUrl: 'https://github.com/kamil0103/LifeOS', deployedUrl: 'https://example.invalid' })
    await request(b, 'PUT', '/portfolio/projects/' + project.id + '/details', { name: 'Foreign' }, 404)
    await request(b, 'POST', '/portfolio/projects/' + project.id + '/sync-github', {}, 404)
    await request(a, 'POST', '/portfolio/import', { repoUrl: 'https://github.com.evil.invalid/owner/repo' }, 400)
    if (process.env.LIFEOS_PROFESSIONAL_SKIP_GITHUB !== '1') {
      const repos = await request(a, 'GET', '/portfolio/github/kamil0103/repos'); assert(repos.some(r => r.name.toLowerCase() === 'lifeos'))
      const imports = await Promise.all(['https://github.com/kamil0103/LifeOS', 'https://github.com/KAMIL0103/LifeOS.git', 'https://github.com/kamil0103/LifeOS/'].map(repoUrl => request(a, 'POST', '/portfolio/import', { repoUrl })))
      assert(imports.every(p => p.id === project.id)); const refreshed = await request(a, 'POST', '/portfolio/projects/' + project.id + '/sync-github', {})
      assert.equal(refreshed.description, 'Detailed custom impact'); assert.equal(refreshed.technologies, 'C++, C'); assert.equal(refreshed.deployedUrl, 'https://example.invalid'); assert.equal(refreshed.isFeatured, true)
      console.log('PASS: real GitHub repository list, concurrent canonical import dedupe and curated-field preservation')
    }
    assert.equal(sql(`SELECT COUNT(*) FROM "Projects" WHERE "UserId"='${a.user.id}'`), '1')
    console.log('PASS: concurrent reviewed-skill dedupe, certificate expiry, dates, ownership and atomic portfolio edits')
  } finally {
    for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`)
    console.log('Disposable professional accounts removed')
  }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
