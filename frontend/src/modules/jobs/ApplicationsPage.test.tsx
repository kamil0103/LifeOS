// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'
import ApplicationsPage from './ApplicationsPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async (path: string) => ({ data: path === '/job-applications' ? [{ id: 'owned', jobId: 'job', jobTitle: 'Engineer', company: 'Example', status: 'applied', statusHistory: [] }] : path === '/profile' ? { timeZone: 'UTC' } : [] })), put: vi.fn(async () => ({})) } }))
it('loads and changes status through the tracker route rather than the gateway endpoint', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  try {
    await act(async () => { root.render(<MemoryRouter><ApplicationsPage /></MemoryRouter>); await new Promise(resolve => setTimeout(resolve, 25)) })
    expect(api.get).toHaveBeenCalledWith('/job-applications'); expect(api.get).not.toHaveBeenCalledWith('/applications')
    expect(container.textContent).toContain('Engineer')
    const select = container.querySelector<HTMLSelectElement>('select[aria-label="Status for Example"]')!
    await act(async () => { select.value = 'interview'; select.dispatchEvent(new Event('change', { bubbles: true })) })
    expect(api.put).toHaveBeenCalledWith('/job-applications/owned/status', { status: 'interview' })
  } finally { await act(async () => root.unmount()); container.remove() }
})
it('protects an unsaved interview when closing the application dialog', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
  try {
    await act(async () => { root.render(<MemoryRouter initialEntries={['/jobs/applications?application=owned']}><ApplicationsPage /></MemoryRouter>); await new Promise(resolve => setTimeout(resolve, 25)) })
    await act(async () => { await new Promise(resolve => setTimeout(resolve, 20)) })
    const dialog = document.querySelector('[role="dialog"]')!
    await act(async () => Array.from(dialog.querySelectorAll('button')).find(b => b.textContent === 'Schedule interview')!.click())
    const round = Array.from(dialog.querySelectorAll('label')).find(l => l.textContent?.startsWith('Round'))!.querySelector('input')!
    await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(round, 'Technical interview'); round.dispatchEvent(new Event('input', { bubbles: true })) })
    await act(async () => dialog.querySelector<HTMLButtonElement>('[aria-label="Close dialog"]')!.click())
    expect(confirm).toHaveBeenCalledWith('Discard unsaved application changes?')
    expect(document.querySelector('[role="dialog"]')).not.toBeNull(); expect(round.value).toBe('Technical interview')
  } finally { await act(async () => root.unmount()); confirm.mockRestore(); container.remove() }
})
