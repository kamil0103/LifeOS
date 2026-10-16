// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'
import JobsPage from './JobsPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async (path: string) => ({ data: path === '/jobs' ? [{ id: 'job', title: 'Firmware engineer', company: 'Router Co', description: 'Embedded C++', status: 'saved', source: 'manual', matchScore: 0, createdAt: '2026-10-14' }] : path === '/resume-versions' ? [{ id: 'resume', title: 'Embedded resume' }] : [] })), post: vi.fn(async () => ({ data: { id: 'application' } })) } }))
it('lets the user choose a resume before tracking an application and shows zero scores', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  try {
    await act(async () => { root.render(<MemoryRouter><JobsPage /></MemoryRouter>); await new Promise(resolve => setTimeout(resolve, 30)) })
    expect(container.textContent).toContain('Match: 0%')
    await act(async () => container.querySelector<HTMLButtonElement>('button[title="Track application"]')!.click())
    const dialog = document.querySelector('[role="dialog"]')!
    expect(dialog.textContent).toContain('Embedded resume')
    const select = dialog.querySelector('select')!
    await act(async () => { select.value = 'resume'; select.dispatchEvent(new Event('change', { bubbles: true })) })
    await act(async () => dialog.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    expect(api.post).toHaveBeenCalledWith('/job-applications', { jobId: 'job', resumeVersionId: 'resume', notes: null })
  } finally { await act(async () => root.unmount()); container.remove() }
})
