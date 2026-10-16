// Run: node tests/frontend-refresh.cjs
const assert = require('node:assert/strict')
const fs = require('node:fs')
const vm = require('node:vm')

async function main() {
  const values = new Map([['accessToken', 'expired'], ['refreshToken', 'original']])
  let handler, finish, refreshCalls = 0, retries = 0
  const api = config => { retries++; return Promise.resolve(config) }
  api.interceptors = { request: { use() {} }, response: { use(_, fn) { handler = fn } } }
  const axios = {
    create: () => api,
    post: () => { refreshCalls++; return new Promise(resolve => { finish = resolve }) },
  }
  const source = fs.readFileSync('frontend/src/lib/api.ts', 'utf8')
    .replace("import axios from 'axios'", '')
    .replace("import { notify } from './notify'", '')
    .replace('import.meta.env.VITE_API_BASE_URL', "''")
    .replace('let refreshInFlight: Promise<string> | null', 'let refreshInFlight')
    .replace(' as string', '')
    .replace('export default api', '')
  vm.runInNewContext(source, {
    axios, notify() {}, FormData: class {}, window: { location: {} },
    localStorage: { getItem: k => values.get(k), setItem: (k, v) => values.set(k, v), removeItem: k => values.delete(k) },
  })
  const error = () => ({ response: { status: 401 }, config: { headers: { Authorization: 'Bearer expired' } } })
  const pending = [handler(error()), handler(error()), handler(error())]
  assert.equal(refreshCalls, 1, 'concurrent failures must share one refresh')
  finish({ data: { accessToken: 'new-access', refreshToken: 'new-refresh' } })
  await Promise.all(pending)
  assert.equal(retries, 3)
  assert.equal(values.get('refreshToken'), 'new-refresh')
  await handler(error())
  assert.equal(refreshCalls, 1, 'a delayed stale response must use the already refreshed token')
  assert.equal(retries, 4)
  console.log('PASS: concurrent refresh and delayed stale response')
}
main().catch(e => { console.error(e); process.exitCode = 1 })
