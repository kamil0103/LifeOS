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
    expect(api.put).toHaveBeenCalledWith('/companynotes/note', expect.objectContaining({ companyName: 'Router Co', notes: 'Products and interview prep', rating: 3, website: '', industry: '', products: '', technologyStack: '', culture: '', interviewPreparation: '' }))
  } finally { await act(async () => root.unmount()); client.clear(); container.remove() }
})

it('shows related applications and retains structured edits after a conflict', async () => {
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path).endsWith('/jobs') ? [{ id: 'job', title: 'Firmware engineer', status: 'saved', applicationId: 'app', applicationStatus: 'interview' }] : [{ id: 'note', companyName: 'Router Co', products: 'Routers', technologyStack: 'C++', interviewPreparation: 'Interrupts', updatedAt: '2026-10-16T00:00:00Z', createdAt: '2026-10-14' }] }))
  vi.mocked(api.put).mockRejectedValueOnce({ response: { data: { detail: 'Company research changed since you opened it.' } } })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container); const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  try {
    await act(async () => { root.render(<QueryClientProvider client={client}><MemoryRouter><CompanyNotesPage /></MemoryRouter></QueryClientProvider>); await new Promise(r => setTimeout(r, 40)) })
    await act(async () => { await new Promise(r => setTimeout(r, 20)) })
    const click = async (text: string) => act(async () => { Array.from(container.querySelectorAll('button')).find(b => b.textContent === text)!.click(); await new Promise(r => setTimeout(r, 20)) })
    await click('View related jobs'); await act(async () => { await new Promise(r => setTimeout(r, 20)) })
    expect(container.querySelector('a[href="/jobs?job=job"]')?.textContent).toBe('Firmware engineer')
    expect(container.querySelector('a[href="/jobs/applications?application=app"]')?.textContent).toContain('interview')
    await click('Edit research'); const form = document.querySelector('[role="dialog"] form')!
    expect(form.textContent).toContain('Interview preparation')
    await act(async () => form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })))
    expect(form.querySelector('[role="alert"]')?.textContent).toContain('changed since')
    expect(Array.from(form.querySelectorAll('textarea')).some(f => f.value === 'Interrupts')).toBe(true)
    expect(api.put).toHaveBeenLastCalledWith('/companynotes/note', expect.objectContaining({ technologyStack: 'C++', expectedUpdatedAt: '2026-10-16T00:00:00Z', rating: null }))
  } finally { await act(async () => root.unmount()); client.clear(); container.remove() }
})
