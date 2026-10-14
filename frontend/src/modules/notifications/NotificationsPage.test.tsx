// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'
import NotificationsPage from './NotificationsPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async (path: string) => ({ data: path === '/notifications/preferences' ? { followUpsEnabled: true, calendarEnabled: true, codingReviewsEnabled: true, calendarLeadMinutes: 30 } : [{ id: 'notice', title: 'Follow up', message: 'Example', actionUrl: '/jobs/applications?application=owned', isRead: false, createdAt: new Date().toISOString() }] })), post: vi.fn(async () => ({})) } }))
it('links to the source and snoozes an actionable reminder', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  try {
    await act(async () => { root.render(<MemoryRouter><NotificationsPage /></MemoryRouter>); await new Promise(resolve => setTimeout(resolve, 30)) })
    expect(container.querySelector('a')?.getAttribute('href')).toBe('/jobs/applications?application=owned')
    await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Snooze 1 hour')!.click())
    expect(api.post).toHaveBeenCalledWith('/notifications/notice/snooze', { minutes: 60 })
    await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Unread')!.click())
    expect(api.get).toHaveBeenCalledWith('/notifications', { params: { unread: true, page: 1 } })
  } finally { await act(async () => root.unmount()); container.remove() }
})
