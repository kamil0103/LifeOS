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
