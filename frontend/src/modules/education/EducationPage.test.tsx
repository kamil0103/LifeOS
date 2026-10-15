// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it, vi } from 'vitest'
import EducationPage from './EducationPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), post: vi.fn() } }))
it('requires review and retains extracted data on save failure', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [] })
  vi.mocked(api.post).mockResolvedValueOnce({ data: { institution: { name: 'Synthetic school' }, degree: { name: 'BSc' }, courses: [{ name: 'Algorithms' }] } })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const click = async (text: string) => act(async () => { Array.from(document.querySelectorAll('button')).find(b => b.textContent?.includes(text))!.click() })
  try {
    await act(async () => root.render(<EducationPage />))
    await act(async () => { const field = container.querySelector('textarea')!; Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!.call(field, 'Synthetic transcript text for testing review before saving only.'); field.dispatchEvent(new Event('input', { bubbles: true })) })
    await click('Extract'); expect(api.post).toHaveBeenCalledTimes(1); expect(document.body.textContent).toContain('Algorithms')
    vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Import temporarily unavailable' } } })
    await click('Save to Education'); expect(document.body.textContent).toContain('Import temporarily unavailable'); expect(document.body.textContent).toContain('Algorithms')
  } finally { await act(async () => root.unmount()); container.remove() }
})
