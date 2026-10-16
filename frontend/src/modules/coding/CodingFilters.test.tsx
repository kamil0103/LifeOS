// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it, vi } from 'vitest'
import CodingTrackerPage from './CodingTrackerPage'
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async (url: string) => ({ data: url.includes('stats') ? null : [
  { id: '1', title: 'Array exercise', category: 'Arrays', difficulty: 'easy', isSolved: false },
  { id: '2', title: 'Graph exercise', category: 'Graphs', difficulty: 'hard', isSolved: true },
] })) } }))
vi.mock('./CodingAttempts', () => ({ default: () => null }))
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
it('combines topic, difficulty and status, preserves options and clears empty results', async () => {
  const el = document.createElement('div'); document.body.append(el); const root = createRoot(el)
  const change = async (label: string, value: string) => { await act(async () => { const select = el.querySelector<HTMLSelectElement>(`[aria-label="${label} filter"]`)!; select.value = value; select.dispatchEvent(new Event('change', { bubbles: true })) }) }
  try {
    await act(async () => root.render(<CodingTrackerPage />))
    await change('Topic', 'Graphs'); expect(el.textContent).toContain('Graph exercise'); expect(el.textContent).not.toContain('Array exercise')
    await change('Difficulty', 'easy'); expect(el.textContent).toContain('No problems match these filters.')
    expect(el.querySelector('[aria-label="Topic filter"]')?.textContent).toContain('Arrays')
    await change('Difficulty', 'hard'); await change('Status', 'false'); expect(el.textContent).toContain('No problems match')
    await act(async () => Array.from(el.querySelectorAll('button')).find(b => b.textContent === 'Clear filters')!.click())
    expect(el.textContent).toContain('Array exercise'); expect(el.textContent).toContain('Graph exercise')
  } finally { await act(async () => root.unmount()); el.remove() }
})
