// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it, vi } from 'vitest'
import CodingAttempts from './CodingAttempts'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async () => ({ data: [{ id: 'a', solvedAt: '2026-10-15T01:00:00Z', notes: 'Hash map, O(n)', solutionLanguage: 'Rust', timeSpentMinutes: 12, xpEarned: 15 }] })), post: vi.fn() } }))
it('shows saved notes and preserves review input after a failed save', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container); const saved = vi.fn()
  try {
    await act(async () => root.render(<CodingAttempts problemId="one" solved={true} attemptCount={1} onSaved={saved} />))
    await act(async () => container.querySelector('button')!.click())
    expect(container.textContent).toContain('Hash map, O(n)'); expect(container.textContent).toContain('Rust'); expect(container.textContent).toContain('15 XP')
    const notes = container.querySelector('textarea')!
    await act(async () => { Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!.call(notes, 'Need to review collisions'); notes.dispatchEvent(new Event('input', { bubbles: true })) })
    vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Could not save review' } } })
    await act(async () => container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    expect(container.textContent).toContain('Could not save review'); expect(notes.value).toBe('Need to review collisions'); expect(saved).not.toHaveBeenCalled()
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} })
    await act(async () => container.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    expect(api.post).toHaveBeenLastCalledWith('/coding/problems/one/reattempt', { notes: 'Need to review collisions', solutionLanguage: undefined, timeSpentMinutes: undefined }); expect(saved).toHaveBeenCalledOnce()
  } finally { await act(async () => root.unmount()); container.remove() }
})
