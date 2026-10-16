// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it, vi } from 'vitest'
import ExperiencePage from './ExperiencePage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), post: vi.fn() } }))
it('saves structured accomplishments and the explicit resume exclusion, retaining them after failure', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [] }); const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const click = async (text: string) => act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === text)!.click())
  const fill = async (field: HTMLInputElement, value: string) => act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, value); field.dispatchEvent(new Event('input', { bubbles: true })) })
  try {
    await act(async () => root.render(<ExperiencePage />)); await click('Add Experience'); await fill(container.querySelector('input[placeholder="Company"]')!, 'Company'); await fill(container.querySelector('input[placeholder="Job Title"]')!, 'Engineer'); await click('Add accomplishment')
    await fill(Array.from(container.querySelectorAll('label')).find(l => l.textContent?.startsWith('Action 1'))!.querySelector('input')!, 'Built service'); await fill(Array.from(container.querySelectorAll('label')).find(l => l.textContent?.startsWith('Metric 1'))!.querySelector('input')!, '20% measured improvement')
    await act(async () => Array.from(container.querySelectorAll('label')).find(l => l.textContent?.includes('Include this work experience'))!.querySelector('input')!.click())
    vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Try saving again' } } }); await click('Save'); expect(container.textContent).toContain('Try saving again')
    expect(api.post).toHaveBeenCalledWith('/experience/work', expect.objectContaining({ includeInResume: false, accomplishments: [{ action: 'Built service', outcome: '', metric: '20% measured improvement' }], bullets: 'Built service — 20% measured improvement' }))
    expect(Array.from(container.querySelectorAll('label')).find(l => l.textContent?.startsWith('Action 1'))!.querySelector('input')!.value).toBe('Built service')
  } finally { await act(async () => root.unmount()); container.remove() }
})
