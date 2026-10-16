// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, expect, it, vi } from 'vitest'
import CoachConversation from './CoachConversation'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
const data = vi.hoisted(() => ({ history: [] as any[], memories: [] as any[], prefs: {} as any, failNext: false }))
vi.mock('@/lib/api', () => ({ default: {
  get: vi.fn(async (path: string) => ({ data: path.endsWith('/history') ? [...data.history] : path.endsWith('/memories') ? [...data.memories] : path.endsWith('/preferences') ? { ...data.prefs } : { date: '2026-10-14', timeZone: 'America/Los_Angeles', builtAt: new Date().toISOString(), sections: [{ source: 'Saved notes', content: 'Focus on C++' }] } })),
  post: vi.fn(async (path: string, body: any) => { if (path.endsWith('/chat')) { if (data.failNext) { data.failNext = false; throw { response: { data: { detail: 'Provider temporarily unavailable' } } } } data.history.push({ id: 'sent', role: 'user', content: body.message, createdAt: new Date().toISOString() }, { id: 'reply', role: 'assistant', content: 'A contextual reply', createdAt: new Date().toISOString() }); return { data: { response: 'A contextual reply' } } } return { data: {} } }),
  put: vi.fn(async (path: string, body: any) => { if (path.endsWith('/preferences')) data.prefs = body; return {} }),
  delete: vi.fn(async (path: string) => { if (path.endsWith('/history')) data.history = []; return {} }),
} }))
beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); data.history = [{ id: 'old', role: 'user', content: 'My earlier question', createdAt: new Date().toISOString() }]; data.memories = [{ id: 'note', content: 'Focus on C++', updatedAt: new Date().toISOString() }]; data.prefs = { includeProfile: true, includeProgress: true, includeCalendar: true, includeJournal: false }; data.failNext = false })
async function renderCoach() { const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container); const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } }); await act(async () => { root.render(<QueryClientProvider client={client}><CoachConversation /></QueryClientProvider>); await new Promise(resolve => setTimeout(resolve, 30)) }); await act(async () => { await new Promise(resolve => setTimeout(resolve, 25)) }); return { container, close: async () => { await act(async () => root.unmount()); client.clear(); container.remove() } } }
it('restores conversation, keeps journal private by default, and clears chat separately from saved notes', async () => {
  const view = await renderCoach(); vi.spyOn(window, 'confirm').mockReturnValue(true)
  try {
    expect(view.container.textContent).toContain('My earlier question'); expect(view.container.textContent).toContain('Focus on C++')
    const checkboxes = view.container.querySelectorAll<HTMLInputElement>('input[type="checkbox"]'); expect(checkboxes[3].checked).toBe(false)
    await act(async () => checkboxes[3].click()); expect(api.put).toHaveBeenCalledWith('/aicoach/preferences', { includeProfile: true, includeProgress: true, includeCalendar: true, includeJournal: true })
    await act(async () => Array.from(view.container.querySelectorAll('button')).find(b => b.textContent === 'Clear conversation')!.click())
    expect(api.delete).toHaveBeenCalledWith('/aicoach/history'); expect(view.container.textContent).not.toContain('My earlier question'); expect(view.container.textContent).toContain('Focus on C++')
  } finally { await view.close(); vi.restoreAllMocks() }
})
it('keeps the draft and retry identifier across a provider failure and page reload', async () => {
  localStorage.setItem('lifeos-coach-draft:current', 'What should I practice next?'); data.failNext = true
  let view = await renderCoach()
  try {
    let form = view.container.querySelector('section[aria-label="Coach conversation"] form')!
    await act(async () => form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    expect(view.container.textContent).toContain('Provider temporarily unavailable'); expect(localStorage.getItem('lifeos-coach-draft:current')).toBe('What should I practice next?')
    await view.close(); view = await renderCoach(); form = view.container.querySelector('section[aria-label="Coach conversation"] form')!
    await act(async () => form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    expect(vi.mocked(api.post).mock.calls[0][1]).toEqual(vi.mocked(api.post).mock.calls[1][1]); expect(view.container.textContent).toContain('A contextual reply'); expect(localStorage.getItem('lifeos-coach-draft:current')).toBeNull()
  } finally { await view.close() }
})
