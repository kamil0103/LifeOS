// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'
import DocumentsPage from './DocumentsPage'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async () => ({ data: [{ id: 'letter', type: 'cover_letter', filename: 'owned_letter.pdf', jobId: 'owned', jobTitle: 'Engineer', generatedAt: '2026-10-14T12:00:00Z' }, { id: 'resume', type: 'resume', filename: 'other_resume.pdf', generatedAt: '2026-10-14T12:00:00Z' }] })) } }))
it('opens the documents for the linked job without mixing unrelated files', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  try {
    await act(async () => { root.render(<MemoryRouter initialEntries={['/documents?job=owned']}><DocumentsPage /></MemoryRouter>); await new Promise(resolve => setTimeout(resolve, 25)) })
    expect(container.textContent).toContain('owned_letter.pdf'); expect(container.textContent).not.toContain('other_resume.pdf')
    expect(container.querySelector('a[href="/jobs?job=owned"]')).not.toBeNull()
  } finally { await act(async () => root.unmount()); container.remove() }
})
