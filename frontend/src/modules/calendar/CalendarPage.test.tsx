// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { afterEach, expect, it, vi } from 'vitest'
import CalendarPage from './CalendarPage'
import { dateTimeInput } from '@/lib/calendar-time'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
const start = new Date().toISOString()
const end = new Date(Date.now() + 3600000).toISOString()
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async (path: string) => ({ data: path === '/profile' ? { timeZone: 'America/Los_Angeles' } : path === '/googlecalendar/status' ? { configured: false, connected: false, calendars: [] } : path === '/googlecalendar/events' ? [] : [{ id: 'native-event', title: 'Test interview', startTime: new Date().toISOString(), endTime: new Date(Date.now() + 3600000).toISOString(), isAllDay: false, eventType: 'job' }] })), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
afterEach(() => vi.restoreAllMocks())
it('opens an existing event with editable details in the profile timezone', async () => {
  const container = document.createElement('div'); document.body.appendChild(container)
  const root = createRoot(container)
  try {
    await act(async () => { root.render(<CalendarPage />); await new Promise(resolve => setTimeout(resolve, 25)) })
    const event = container.querySelector<HTMLButtonElement>('button[title="Test interview"]')
    expect(event).not.toBeNull()
    await act(async () => event!.click())
    const dialog = document.querySelector('[role="dialog"]')!
    expect(dialog.textContent).toContain('Edit Event')
    const inputs = Array.from(dialog.querySelectorAll<HTMLInputElement>('input'))
    expect(inputs[0].value).toBe('Test interview')
    expect(inputs.some(input => input.value === dateTimeInput(start, 'America/Los_Angeles') || input.value === dateTimeInput(end, 'America/Los_Angeles'))).toBe(true)
    expect(inputs[0].disabled).toBe(false)
    expect(dialog.textContent).toContain('Delete')
  } finally { await act(async () => root.unmount()); container.remove() }
})
