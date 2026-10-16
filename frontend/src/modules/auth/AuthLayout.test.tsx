// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import AuthLayout from './AuthLayout'
import api from '@/lib/api'
import { useAuthStore } from '@/hooks/useAuthStore'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn() } }))
beforeEach(() => { localStorage.clear(); localStorage.setItem('accessToken', 'saved-access'); localStorage.setItem('refreshToken', 'saved-refresh'); useAuthStore.setState({ user: null, isAuthenticated: false, isLoading: true }); vi.mocked(api.get).mockReset() })
const tree = <MemoryRouter initialEntries={['/jobs']}><Routes><Route element={<AuthLayout />}><Route path="/jobs" element={<p>Protected jobs</p>} /><Route path="/login" element={<p>Login form</p>} /></Route></Routes></MemoryRouter>
it('restores the saved session on a page reload using the dedicated route', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: { user: { id: 'owned-user', username: 'test', email: 'test@example.invalid', role: 'user' } } })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  try { await act(async () => { root.render(tree); await new Promise(resolve => setTimeout(resolve, 20)) }); expect(api.get).toHaveBeenCalledWith('/lifeos/session'); expect(container.textContent).toContain('Protected jobs'); expect(localStorage.getItem('refreshToken')).toBe('saved-refresh') } finally { await act(async () => root.unmount()); container.remove() }
})
it('preserves tokens during a connection failure and restores the session on retry', async () => {
  vi.mocked(api.get).mockRejectedValueOnce(new Error('Offline')).mockResolvedValueOnce({ data: { user: { id: 'owned-user', username: 'test', email: 'test@example.invalid', role: 'user' } } })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  try { await act(async () => { root.render(tree); await new Promise(resolve => setTimeout(resolve, 20)) }); expect(container.textContent).toContain('Your login is saved'); expect(localStorage.getItem('accessToken')).toBe('saved-access'); expect(localStorage.getItem('refreshToken')).toBe('saved-refresh'); await act(async () => container.querySelector('button')!.click()); expect(container.textContent).toContain('Protected jobs') } finally { await act(async () => root.unmount()); container.remove() }
})
