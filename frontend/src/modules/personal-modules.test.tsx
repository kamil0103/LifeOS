// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { beforeEach, expect, it, vi } from 'vitest'
import AnalyticsPage from './analytics/AnalyticsPage'
import BiblePage from './bible/BiblePage'
import JournalPage from './journal/JournalPage'
import RecipesPage from './recipes/RecipesPage'
import { scaleVariable } from './recipes/recipe-scaling'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), put: vi.fn(), post: vi.fn(), delete: vi.fn() } }))
beforeEach(() => { vi.resetAllMocks(); localStorage.clear(); sessionStorage.clear(); vi.spyOn(window, 'confirm').mockReturnValue(true) })
async function mount(page: React.ReactNode) {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  await act(async () => root.render(page))
  return { container, click: async (text: string) => { await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === text)!.click()) }, close: async () => { await act(async () => root.unmount()); container.remove() } }
}
async function setField(field: HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement, value: string) {
  const type = field instanceof HTMLTextAreaElement ? HTMLTextAreaElement : field instanceof HTMLSelectElement ? HTMLSelectElement : HTMLInputElement
  await act(async () => { Object.getOwnPropertyDescriptor(type.prototype, 'value')!.set!.call(field, value); field.dispatchEvent(new Event(type === HTMLSelectElement ? 'change' : 'input', { bubbles: true })) })
}
it('analytics renders zero days, distinct coding counts and a selectable common period', async () => {
  vi.mocked(api.get).mockImplementation(async path => { const days = Number(String(path).split('=')[1]); return { data: { startDate: '2026-10-01', endDate: '2026-10-14', timeZone: 'America/Los_Angeles', xpTrends: [], habitHeatmap: Array.from({ length: days }, (_, i) => ({ date: '2026-10-' + String(i + 1).padStart(2, '0'), count: 0, habits: [] })), jobFunnel: { saved: 2, applied: 0, phone_screen: 0, interview: 0, offer: 0, rejected: 0, withdrawn: 1, total: 1 }, codingStreaks: { current: 2, longest: 4, totalDays: 5, totalProblems: 1, totalAttempts: 7 }, overview: { totalXp: 0, habitsCount: 0, jobsCount: 3, codingCount: 1, docsCount: 0 } } } })
  const view = await mount(<AnalyticsPage />)
  try { expect(view.container.querySelectorAll('[role="img"]')).toHaveLength(30); expect(view.container.textContent).toContain('Withdrawn'); expect(view.container.textContent).toContain('7 total attempts'); await setField(view.container.querySelector('select')!, '7'); expect(view.container.querySelectorAll('[role="img"]')).toHaveLength(7); expect(api.get).toHaveBeenLastCalledWith('/analytics/report?days=7', expect.any(Object)) } finally { await view.close() }
})
it('Bible restores chapter position and retains failed bookmark notes', async () => {
  localStorage.setItem('lifeos-bible-position:current', JSON.stringify({ bookId: 'book', chapter: 2 }))
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path).endsWith('/books') ? [{ id: 'book', name: 'Test', testament: 'OT', chapterCount: 2 }] : String(path).includes('/chapters/') ? { verses: [{ id: 'verse', verseNumber: 1, text: 'Verse', isBookmarked: false }] } : String(path).endsWith('/bookmarks') ? [] : { id: 'verse', reference: 'Test 2:1', text: 'Verse' } }))
  const view = await mount(<BiblePage />)
  try {
    expect(api.get).toHaveBeenCalledWith('/bible/books/book/chapters/2', expect.any(Object))
    await act(async () => view.container.querySelector<HTMLButtonElement>('[aria-label="Bookmark verse 1"]')!.click())
    await setField(view.container.querySelector('textarea')!, 'My reflection'); vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Temporary save error' } } })
    await view.click('Save'); expect(view.container.textContent).toContain('Temporary save error'); expect(view.container.querySelector('textarea')!.value).toBe('My reflection')
  } finally { await view.close() }
})
it('journal restores an edit draft to its original entry and sends its local date', async () => {
  const entry = { id: 'entry', title: 'Original', content: 'Original', localDate: '2026-10-14', entryDate: '2026-10-14T07:00:00Z', updatedAt: '2026-10-15T01:00:00Z' }
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path) === '/journal' ? [entry] : String(path) === '/profile' ? { timeZone: 'America/Los_Angeles' } : {} }))
  // The auth store has no user in this fixture, matching the page's fallback draft key.
  sessionStorage.setItem('lifeos-journal-draft-undefined', JSON.stringify({ entryId: 'entry', expectedUpdatedAt: entry.updatedAt, requestId: crypto.randomUUID(), form: { title: 'Recovered', content: 'Recovered text', entryDate: '2026-10-14', mood: '' } }))
  vi.mocked(api.put).mockResolvedValue({ data: {} }); const view = await mount(<JournalPage />)
  try { await view.click('New Entry'); expect(view.container.textContent).toContain('Edit Entry'); await view.click('Save Entry'); expect(api.put).toHaveBeenCalledWith('/journal/entry', expect.objectContaining({ title: 'Recovered', date: '2026-10-14', expectedUpdatedAt: entry.updatedAt })); expect(api.post).not.toHaveBeenCalled() } finally { await view.close() }
})
it('recipe drafts retain the same retry identifier after a failed save and reload', async () => {
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path) === '/recipes' ? [] : {} }))
  const requestId = crypto.randomUUID(); sessionStorage.setItem('lifeos-recipe-draft:current', JSON.stringify({ requestId, form: { name: 'Recovered soup', description: '', category: '', baseServings: 2, prepTime: '', cookTime: '' }, ingredients: [{ name: 'Water', quantity: 1, unit: 'cup' }], steps: [{ stepNumber: 1, stepType: 'cook', text: 'Cook', variables: [] }] }))
  vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Temporary recipe error' } } }); let view = await mount(<RecipesPage />)
  try {
    await view.click('New Recipe'); await view.click('Save Recipe'); expect(view.container.textContent).toContain('Temporary recipe error'); expect(sessionStorage.getItem('lifeos-recipe-draft:current')).toContain(requestId)
    await view.close(); view = await mount(<RecipesPage />); await view.click('New Recipe'); vi.mocked(api.post).mockResolvedValueOnce({ data: {} }); await view.click('Save Recipe'); expect(vi.mocked(api.post).mock.calls[0][1]).toEqual(vi.mocked(api.post).mock.calls[1][1])
  } finally { await view.close() }
})
it('serving changes keep temperatures fixed while honoring explicit ingredient-style scaling', () => {
  expect(scaleVariable({ name: 'temperature', value: 350, unit: '°F', scalingMode: 'linear' }, 2)).toBe(350)
  expect(scaleVariable({ name: 'custom', value: 180, unit: '°C', scalingMode: 'sqrt' }, 4)).toBe(180)
  expect(scaleVariable({ name: 'water', value: 2, unit: 'cup', scalingMode: 'linear' }, 3)).toBe(6)
})
