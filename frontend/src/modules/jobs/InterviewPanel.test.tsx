// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import InterviewPanel from './InterviewPanel'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }))
beforeEach(() => vi.resetAllMocks())
async function mount() {
  const el = document.createElement('div'); document.body.appendChild(el); const root = createRoot(el); const dirty = vi.fn(), busy = vi.fn()
  await act(async () => root.render(<MemoryRouter><InterviewPanel applicationId="app" zone="America/Los_Angeles" onDirty={dirty} onBusy={busy} /></MemoryRouter>))
  return { el, dirty, click: async (text: string) => act(async () => { Array.from(el.querySelectorAll('button')).find(b => b.textContent === text)!.click() }), close: async () => { await act(async () => root.unmount()); el.remove() } }
}
async function input(el: HTMLElement, label: string, value: string) {
  const node = Array.from(el.querySelectorAll('label')).find(l => l.textContent?.startsWith(label))!.querySelector('input, textarea')!
  await act(async () => { Object.getOwnPropertyDescriptor(node instanceof HTMLInputElement ? HTMLInputElement.prototype : HTMLTextAreaElement.prototype, 'value')!.set!.call(node, value); node.dispatchEvent(new Event('input', { bubbles: true })) })
}
async function submit(el: HTMLElement) { await act(async () => el.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))) }
it('uses the profile timezone and a stable retry identifier, retaining failed interview data', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [] }); vi.mocked(api.post).mockRejectedValue({ response: { data: { detail: 'Calendar conflict' } } })
  const view = await mount()
  try {
    await view.click('Schedule interview'); await input(view.el, 'Round', 'Technical'); await input(view.el, 'Interview start', '2026-11-02T10:00'); await input(view.el, 'Interview end', '2026-11-02T11:00'); await input(view.el, 'Preparation notes', 'Interrupts')
    await submit(view.el); expect(view.el.textContent).toContain('Calendar conflict'); expect(view.dirty).toHaveBeenCalledWith(true)
    expect(api.post).toHaveBeenCalledWith('/job-applications/app/interviews', expect.objectContaining({ round: 'Technical', startTime: '2026-11-02T18:00:00.000Z', endTime: '2026-11-02T19:00:00.000Z', preparation: 'Interrupts' }))
    const requestId = (vi.mocked(api.post).mock.calls[0][1] as { requestId: string }).requestId; await submit(view.el); expect((vi.mocked(api.post).mock.calls[1][1] as { requestId: string }).requestId).toBe(requestId)
    expect(view.el.querySelector('textarea')?.value).toBe('Interrupts')
  } finally { await view.close() }
})
it('shows history and links, sends revisioned cancellations and rejects impossible DST times', async () => {
  const item = { id: 'i', round: 'Screen', format: 'phone', status: 'scheduled', startTime: '2026-03-08T18:00:00Z', endTime: '2026-03-08T19:00:00Z', updatedAt: '2026-01-01T00:00:00Z', calendarEventId: 'event', history: [{ round: 'Screen', format: 'phone', status: 'scheduled', startTime: '2026-03-08T18:00:00Z', endTime: '2026-03-08T19:00:00Z', changedAt: '2026-01-01T00:00:00Z', outcome: 'Original note' }] }
  vi.mocked(api.get).mockResolvedValue({ data: [item] }); vi.mocked(api.put).mockResolvedValue({})
  const view = await mount()
  try {
    expect(view.el.textContent).toContain('Original note'); expect(view.el.querySelector('a')?.getAttribute('href')).toBe('/calendar?date=2026-03-08&event=event')
    await view.click('Edit interview'); await input(view.el, 'Interview start', '2026-03-08T02:30'); await submit(view.el); expect(view.el.textContent).toContain('daylight-saving'); expect(api.put).not.toHaveBeenCalled()
    await input(view.el, 'Interview start', '2026-03-08T11:00')
    const status = Array.from(view.el.querySelectorAll('label')).find(l => l.textContent?.startsWith('Interview status'))!.querySelector('select')!
    await act(async () => { status.value = 'cancelled'; status.dispatchEvent(new Event('change', { bubbles: true })) }); await submit(view.el)
    expect(api.put).toHaveBeenCalledWith('/job-applications/app/interviews/i', expect.objectContaining({ status: 'cancelled', expectedUpdatedAt: item.updatedAt }))
  } finally { await view.close() }
})
