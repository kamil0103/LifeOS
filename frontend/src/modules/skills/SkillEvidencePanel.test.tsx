// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { beforeEach, expect, it, vi } from 'vitest'
import SkillEvidencePanel from './SkillEvidencePanel'
import SkillsPage from './SkillsPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
beforeEach(() => vi.resetAllMocks())
async function mount(page: React.ReactNode) { const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container); await act(async () => root.render(page)); return async () => { await act(async () => root.unmount()); container.remove() } }
async function click(text: string) { await act(async () => Array.from(document.querySelectorAll('button')).find(b => b.textContent === text)!.click()) }
it('links only after explicit save and retains notes when linking fails', async () => {
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path).endsWith('evidence-options') ? [{ type: 'project', id: 'p', label: 'Firmware project', url: '/experience' }] : [] }))
  const close = await mount(<SkillEvidencePanel skill={{ id: 's', name: 'C++' }} onClose={vi.fn()} />)
  try {
    expect(api.post).not.toHaveBeenCalled()
    await act(async () => { const field = document.querySelector('select')!; field.value = 'project:p'; field.dispatchEvent(new Event('change', { bubbles: true })); const notes = document.querySelector('textarea')!; Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!.call(notes, 'Built a driver'); notes.dispatchEvent(new Event('input', { bubbles: true })) })
    vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Evidence save unavailable' } } }); await click('Link evidence')
    expect(document.body.textContent).toContain('Evidence save unavailable'); expect(document.querySelector('textarea')!.value).toBe('Built a driver'); expect(api.post).toHaveBeenCalledWith('/skills/s/evidence', { sourceType: 'project', sourceId: 'p', notes: 'Built a driver' })
  } finally { await close() }
})
it('shows a deleted source without a link and allows unlinking', async () => {
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path).endsWith('evidence-options') ? [] : [{ id: 'e', label: 'Source no longer available', notes: 'Old evidence note', available: false }] }))
  const close = await mount(<SkillEvidencePanel skill={{ id: 's', name: 'C++' }} onClose={vi.fn()} />)
  try { expect(document.body.textContent).toContain('Old evidence note'); expect(document.querySelector('a')).toBeNull(); await click('Unlink Source no longer available'); expect(api.delete).toHaveBeenCalledWith('/skills/s/evidence/e') } finally { await close() }
})
it('saves the explicit learning status without inferring it from proficiency', async () => {
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path).endsWith('certificates') ? [] : [{ id: 's', name: 'C++', proficiency: 'Advanced', category: 'Programming Language', learningStatus: 'unspecified' }] }))
  const close = await mount(<SkillsPage />)
  try { await act(async () => document.querySelector<HTMLButtonElement>('[aria-label="Edit C++"]')!.click()); await act(async () => { const select = document.querySelector<HTMLSelectElement>('[aria-label="Skill status"]')!; select.value = 'learning'; select.dispatchEvent(new Event('change', { bubbles: true })) }); await click('Save'); expect(api.put).toHaveBeenCalledWith('/skills/s', expect.objectContaining({ proficiency: 'Advanced', learningStatus: 'learning' })) } finally { await close() }
})
