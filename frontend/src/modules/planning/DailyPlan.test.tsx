// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'
import DailyPlan from './DailyPlan'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async (path: string) => ({ data: path === '/googlecalendar/status' ? { connected: true } : { date: '2026-10-14', timeZone: 'UTC', actions: [{ id: 'habit:owned', kind: 'habit', sourceId: 'owned', title: 'Walk', reason: 'Health', completed: false, canComplete: true, actionUrl: '/habits' }], commitments: [{ id: 'event', title: 'Interview', source: 'LifeOS local', startTime: '2026-10-14T16:00:00Z', isAllDay: false, actionUrl: '/calendar?event=event' }] } })), post: vi.fn(async () => ({ data: {} })) } }))
it('shows commitments, completes habits through their API, and offers both calendars', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  try {
    await act(async () => { root.render(<QueryClientProvider client={client}><MemoryRouter><DailyPlan /></MemoryRouter></QueryClientProvider>); await new Promise(resolve => setTimeout(resolve, 50)) })
    await act(async () => { await new Promise(resolve => setTimeout(resolve, 30)) })
    expect(container.textContent).toContain('Walk'); expect(container.querySelector('a[href="/calendar?event=event"]')).not.toBeNull()
    await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Complete')!.click())
    expect(api.post).toHaveBeenCalledWith('/habits/owned/complete')
    await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Schedule')!.click())
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('LifeOS in Google Calendar')
  } finally { await act(async () => root.unmount()); client.clear(); container.remove() }
})
