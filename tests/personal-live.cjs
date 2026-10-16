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
async function raceUpdates(a, path, payload) {
  const statuses = await Promise.all([1,2].map(index => fetch(base + path, { method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + a.accessToken }, body: JSON.stringify({ ...payload, title: payload.title ? payload.title + index : undefined, name: payload.name ? payload.name + index : undefined }) }).then(r => r.status)))
  assert.deepEqual(statuses.sort(), [200,409])
}
;(async () => {
  try {
    const a = await account(), b = await account()
    const journalBody = { requestId: crypto.randomUUID(), title: 'Private synthetic reflection', content: 'Synthetic text only', date: '2026-11-02' }
    const entries = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/journal', journalBody)))
    assert.equal(new Set(entries.map(e => e.id)).size, 1); assert(entries.every(e => e.localDate === '2026-11-02')); assert.equal(Date.parse(entries[0].entryDate), Date.parse('2026-11-02T08:00:00Z'))
    assert.equal((await request(a, 'GET', '/journal')).length, 1); assert.equal((await request(b, 'GET', '/journal')).length, 0)
    await request(b, 'PUT', '/journal/' + entries[0].id, { title: 'Foreign', content: 'Foreign' }, 404)
    await raceUpdates(a, '/journal/' + entries[0].id, { ...journalBody, expectedUpdatedAt: entries[0].updatedAt })
    await request(a, 'POST', '/journal', { ...journalBody, content: 'Changed replay' }, 400)
    await request(a, 'POST', '/journal', { title: 'Invalid date', date: '2026-02-30' }, 400)
    await request(b, 'PUT', '/profile', { fullName: 'Synthetic Tokyo account', timeZone: 'Asia/Tokyo' })
    const tokyo = await request(b, 'POST', '/journal', { title: 'Tokyo date', date: '2026-11-02' }); assert.equal(tokyo.localDate, '2026-11-02'); assert.equal(Date.parse(tokyo.entryDate), Date.parse('2026-11-01T15:00:00Z'))
    const recipeBody = { requestId: crypto.randomUUID(), name: 'Synthetic soup', baseServings: 2, ingredients: [{ name: 'Water', quantity: 2, unit: 'cup' }], steps: [{ stepType: 'cook', text: 'Heat', variables: [{ name: 'temperature', value: 350, unit: '°F', scalingMode: 'linear' }] }] }
    const recipes = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/recipes', recipeBody)))
    assert.equal(new Set(recipes.map(r => r.id)).size, 1); assert(recipes.every(r => r.ingredients.length === 1)); assert.equal(recipes[0].steps[0].variables[0].scalingMode, 'none')
    assert.equal(sql(`SELECT COUNT(*) FROM "RecipeIngredients" WHERE "RecipeId"='${recipes[0].id}'`), '1')
    const update = { ...recipeBody, expectedUpdatedAt: recipes[0].updatedAt, ingredients: [{ name: 'Rice', quantity: 1 }, { name: 'Water', quantity: 3 }], steps: [{ stepType: 'prep', text: 'Mix', variables: [] }, { stepType: 'cook', text: 'Cook', variables: [] }] }
    await raceUpdates(a, '/recipes/' + recipes[0].id, update)
    const edited = await request(a, 'GET', '/recipes/' + recipes[0].id); assert.deepEqual(edited.ingredients.map(i => i.name), ['Rice','Water']); assert.deepEqual(edited.steps.map(s => s.stepNumber), [1,2])
    await request(b, 'GET', '/recipes/' + recipes[0].id, undefined, 404); await request(b, 'DELETE', '/recipes/' + recipes[0].id, undefined, 404)
    await request(a, 'POST', '/recipes', { name: 'Invalid', baseServings: -1 }, 400)
    const books = await request(a, 'GET', '/bible/books'); assert(books.length > 0, 'Bible seed is required for the reader verification')
    const chapter = await request(a, 'GET', '/bible/books/' + books[0].id + '/chapters/1'); assert(chapter.verses.length > 0)
    const verse = chapter.verses[0], bookmarkBody = { verseId: verse.id, note: 'Synthetic note', color: 'purple' }
    const marks = await Promise.all(Array.from({ length: 6 }, () => request(a, 'POST', '/bible/bookmarks', bookmarkBody)))
    assert.equal(new Set(marks.map(b => b.id)).size, 1); assert(marks.every(m => m.reference && m.bookId === books[0].id)); assert.equal((await request(a, 'GET', '/bible/bookmarks')).length, 1)
    await request(b, 'DELETE', '/bible/bookmarks/' + marks[0].id, undefined, 404)
    assert.equal((await request(a, 'GET', '/bible/daily')).id, (await request(a, 'GET', '/bible/daily')).id)
    await request(a, 'GET', '/bible/books/' + books[0].id + '/chapters/999', undefined, 400)
    const report = await request(a, 'GET', '/analytics/report?days=7'); assert.equal(report.xpTrends.length, 7); assert.equal(report.habitHeatmap.length, 7); assert(report.xpTrends.every(d => d.xp === 0)); assert.equal(report.timeZone, 'America/Los_Angeles')
    assert.equal((await request(b, 'GET', '/analytics/report?days=7')).timeZone, 'Asia/Tokyo')
    console.log('PASS: concurrent journal/recipe retry dedupe, stale-edit conflicts, local dates/DST, child replacement, ownership, bookmark saves and zero-filled analytics')
  } finally {
    for (const id of users) sql(`DELETE FROM "Users" WHERE "Id"='${id}'`)
    console.log('Disposable personal-module accounts removed')
  }
})().catch(error => { console.error(error.message); process.exitCode = 1 })
