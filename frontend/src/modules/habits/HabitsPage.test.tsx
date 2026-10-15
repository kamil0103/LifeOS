// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { beforeEach, expect, it, vi } from 'vitest'
import HabitsPage from './HabitsPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
const habit = { id: 'h', name: 'Read', category: 'Reading', targetValue: 10, unit: 'pages', scheduleDaysMask: 127, isActive: true, isDueToday: true, isCompleted: false, todayValue: 4, currentStreak: 0, longestStreak: 2, updatedAt: '2026-10-15T10:00:00Z', timeZone: 'America/Los_Angeles' }
beforeEach(() => vi.resetAllMocks())
async function mount() { const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container); await act(async () => root.render(<HabitsPage />)); return { close: async () => { await act(async () => root.unmount()); container.remove() } } }
async function click(text: string) { await act(async () => Array.from(document.querySelectorAll('button')).find(b => b.textContent === text)!.click()) }
async function input(selector: string, value: string) { await act(async () => { const field = document.querySelector<HTMLInputElement>(selector)!; Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, value); field.dispatchEvent(new Event('input', { bubbles: true })) }) }
it('edits schedules and keeps form values after failure', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [habit] }); const view = await mount()
  try {
    await click('Edit Read'); await input('input[maxlength="200"]', 'Read more'); await click('Weekdays')
    vi.mocked(api.put).mockRejectedValueOnce({ response: { data: { detail: 'Version changed' } } }); await click('Save habit')
    expect(document.body.textContent).toContain('Version changed'); expect(document.querySelector<HTMLInputElement>('input[maxlength="200"]')!.value).toBe('Read more')
    expect(api.put).toHaveBeenCalledWith('/habits/h', expect.objectContaining({ name: 'Read more', scheduleDaysMask: 62, expectedUpdatedAt: habit.updatedAt, targetValue: 10 }))
  } finally { await view.close() }
})
it('submits the numeric total and does not use the boolean completion endpoint', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [habit] }); vi.mocked(api.post).mockResolvedValue({ data: { xpEarned: 0 } }); const view = await mount()
  try { await input('[aria-label="Today\'s total for Read"]', '7'); await click('Save progress'); expect(api.post).toHaveBeenCalledWith('/habits/h/progress', { value: 7 }); expect(document.body.textContent).toContain('total, not an amount to add') } finally { await view.close() }
})
it('archives with confirmation and restores from the archived list', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [habit] }); const view = await mount()
  try {
    await click('Archive'); expect(api.delete).not.toHaveBeenCalled(); expect(document.body.textContent).toContain('history and XP are kept'); await click('Archive habit'); expect(api.delete).toHaveBeenCalledWith('/habits/h')
    vi.mocked(api.get).mockResolvedValue({ data: [{ ...habit, isActive: false, isDueToday: false }] }); await click('Archived'); await click('Restore Read'); expect(api.post).toHaveBeenCalledWith('/habits/h/restore')
  } finally { await view.close() }
})
it('hides off-day habits in Today, keeps them editable in All active and reports load failure', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [{ ...habit, isDueToday: false }] }); const view = await mount()
  try { expect(document.body.textContent).toContain('No habits scheduled today'); await click('All active'); expect(document.body.textContent).toContain('Edit Read'); expect(document.body.textContent).toContain('Not scheduled today'); vi.mocked(api.get).mockRejectedValueOnce(new Error('offline')); await click('Today'); expect(document.body.textContent).toContain('Could not load habits') } finally { await view.close() }
})
