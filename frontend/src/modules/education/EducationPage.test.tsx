// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { beforeEach, expect, it, vi } from 'vitest'
import EducationPage from './EducationPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), post: vi.fn(), request: vi.fn() } }))
beforeEach(() => vi.resetAllMocks())
it('requires review and retains extracted data on save failure', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [] })
  vi.mocked(api.post).mockResolvedValueOnce({ data: { institution: { name: 'Synthetic school' }, degree: { name: 'BSc' }, courses: [{ name: 'Algorithms' }] } })
  vi.mocked(api.post).mockResolvedValueOnce({ data: [{ index: 0, status: 'new' }] })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const click = async (text: string) => act(async () => { Array.from(document.querySelectorAll('button')).find(b => b.textContent?.includes(text))!.click() })
  try {
    await act(async () => root.render(<EducationPage />))
    await act(async () => { const field = container.querySelector('textarea')!; Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!.call(field, 'Synthetic transcript text for testing review before saving only.'); field.dispatchEvent(new Event('input', { bubbles: true })) })
    await click('Extract'); expect(api.post).toHaveBeenCalledTimes(2); expect(document.body.textContent).toContain('Algorithms')
    vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Import temporarily unavailable' } } })
    await click('Save to Education'); expect(document.body.textContent).toContain('Import temporarily unavailable'); expect(document.body.textContent).toContain('Algorithms')
  } finally { await act(async () => root.unmount()); container.remove() }
})
it('creates degrees and courses with owned parents and retains failed form input', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [{ id: 'school', name: 'School', institutionType: 'university', degrees: [], unassignedCourses: [] }] })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const click = async (text: string) => act(async () => { Array.from(container.querySelectorAll('button')).find(b => b.textContent?.trim() === text)!.click() })
  const fill = async (placeholder: string, value: string) => act(async () => { const field = container.querySelector<HTMLInputElement>(`input[placeholder="${placeholder}"]`)!; Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, value); field.dispatchEvent(new Event('input', { bubbles: true })) })
  try {
    await act(async () => root.render(<EducationPage />)); await click('Add Degree')
    await fill('Degree Name *', 'BSc Computing')
    vi.mocked(api.request).mockRejectedValueOnce({ response: { data: { detail: 'Try again later' } } })
    await click('Save'); expect(container.textContent).toContain('Try again later'); expect(container.querySelector<HTMLInputElement>('input[placeholder="Degree Name *"]')!.value).toBe('BSc Computing')
    vi.mocked(api.request).mockResolvedValueOnce({ data: {} }); await click('Save')
    expect(api.request).toHaveBeenLastCalledWith(expect.objectContaining({ method: 'post', url: '/education/degrees', data: expect.objectContaining({ institutionId: 'school', degreeName: 'BSc Computing' }) }))
    await click('Add Course'); await fill('Course Name *', 'Algorithms'); await click('Save')
    expect(api.request).toHaveBeenLastCalledWith(expect.objectContaining({ method: 'post', url: '/education/courses', data: expect.objectContaining({ institutionId: 'school', degreeId: null, name: 'Algorithms' }) }))
  } finally { await act(async () => root.unmount()); container.remove() }
})
it('searches course terms and shows a useful empty result', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [{ id: 'school', name: 'School', institutionType: 'university', degrees: [], unassignedCourses: [{ id: 'c', name: 'Algorithms', term: 'Fall 2026', isMajorRelated: true }] }] })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const search = async (value: string) => act(async () => { const field = container.querySelector<HTMLInputElement>('[aria-label="Search education"]')!; Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, value); field.dispatchEvent(new Event('input', { bubbles: true })) })
  try {
    await act(async () => root.render(<EducationPage />)); await search('fall 2026'); expect(container.textContent).toContain('Algorithms')
    await search('missing'); expect(container.textContent).toContain('No matching education records.'); expect(container.textContent).not.toContain('Algorithms')
    await search(''); expect(container.textContent).toContain('Algorithms')
  } finally { await act(async () => root.unmount()); container.remove() }
})
