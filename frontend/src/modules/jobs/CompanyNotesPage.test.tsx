// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { expect, it, vi } from 'vitest'
import CompanyNotesPage from './CompanyNotesPage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(async () => ({ data: [{ id: 'note', companyName: 'Router Co', notes: 'Products and interview prep', rating: 3, createdAt: '2026-10-14' }] })), put: vi.fn(async () => ({})) } }))
it('filters company research from the job link and edits the existing note', async () => {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container); const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  try {
    await act(async () => { root.render(<QueryClientProvider client={client}><MemoryRouter initialEntries={['/jobs/notes?company=Router%20Co']}><CompanyNotesPage /></MemoryRouter></QueryClientProvider>); await new Promise(resolve => setTimeout(resolve, 40)) })
    await act(async () => { await new Promise(resolve => setTimeout(resolve, 20)) })
    expect(container.textContent).toContain('Products and interview prep')
    await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Edit research')!.click())
    const form = document.querySelector('[role="dialog"] form')!
    await act(async () => form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    expect(api.put).toHaveBeenCalledWith('/companynotes/note', { companyName: 'Router Co', notes: 'Products and interview prep', rating: 3 })
  } finally { await act(async () => root.unmount()); client.clear(); container.remove() }
})
