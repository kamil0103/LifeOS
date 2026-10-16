// @vitest-environment jsdom
import { beforeEach, expect, it, vi } from 'vitest'
const state = vi.hoisted(() => ({ handler: null as any, post: vi.fn() }))
vi.mock('axios', () => ({ default: { create: () => ({ interceptors: { request: { use() {} }, response: { use(_success: unknown, handler: unknown) { state.handler = handler } } } }), post: state.post, isAxiosError: (error: any) => !!error.isAxiosError } }))
import './api'
beforeEach(() => { localStorage.clear(); localStorage.setItem('accessToken', 'expired'); localStorage.setItem('refreshToken', 'saved-refresh'); state.post.mockReset() })
it('keeps the saved tokens when refresh encounters an outage', async () => {
  const outage = { isAxiosError: true, response: { status: 503 } }; state.post.mockRejectedValueOnce(outage)
  await expect(state.handler({ response: { status: 401 }, config: { headers: { Authorization: 'Bearer expired' } } })).rejects.toBe(outage)
  expect(localStorage.getItem('accessToken')).toBe('expired'); expect(localStorage.getItem('refreshToken')).toBe('saved-refresh')
})
