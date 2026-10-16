// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { beforeEach, expect, it, vi } from 'vitest'
import PortfolioPage from './portfolio/PortfolioPage'
import SkillsPage from './skills/SkillsPage'
import ExperiencePage from './experience/ExperiencePage'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), put: vi.fn(), post: vi.fn() } }))
beforeEach(() => vi.resetAllMocks())
async function mount(page: React.ReactNode) {
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  await act(async () => root.render(page))
  return { container, click: async (text: string) => { await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent?.includes(text))!.click()) }, close: async () => { await act(async () => root.unmount()); container.remove() } }
}
async function input(container: HTMLElement, placeholder: string, value: string) {
  const field = container.querySelector<HTMLInputElement>(`input[placeholder="${placeholder}"]`)!
  await act(async () => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, value); field.dispatchEvent(new Event('input', { bubbles: true })) })
}
it('uses one atomic portfolio save and retains edits after a save error', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [{ id: 'p', name: 'Original', isCurrent: false, isFeatured: true, gitHubRepoUrl: 'https://github.com/owner/repo' }] })
  const view = await mount(<PortfolioPage />)
  try {
    await act(async () => view.container.querySelector<HTMLButtonElement>('[aria-label="Edit Original"]')!.click())
    await input(view.container, 'Project Name', 'Updated')
    vi.mocked(api.put).mockRejectedValueOnce({ response: { data: { detail: 'Invalid deployment URL' } } })
    await view.click('Save'); expect(view.container.textContent).toContain('Invalid deployment URL'); expect(view.container.querySelector<HTMLInputElement>('input[placeholder="Project Name"]')!.value).toBe('Updated')
    expect(api.put).toHaveBeenCalledTimes(1); expect(vi.mocked(api.put).mock.calls[0][0]).toBe('/portfolio/projects/p/details'); expect(api.post).not.toHaveBeenCalled()
  } finally { await view.close() }
})
it('shows certificate status and saves extracted skills only after user review', async () => {
  vi.mocked(api.get).mockImplementation(async path => ({ data: String(path).endsWith('/certificates') ? [{ id: 'c', name: 'Cloud', expiryStatus: 'expiring_soon' }] : [] }))
  vi.mocked(api.post).mockResolvedValueOnce({ data: [{ name: 'C++', category: 'Programming Language', proficiency: 'Intermediate' }] })
  const view = await mount(<SkillsPage />)
  try {
    await view.click('Certificates ('); expect(view.container.textContent).toContain('Expires within 30 days'); await view.click('Skills (')
    await view.click('Extract'); expect(view.container.textContent).toContain('Review Extracted Skills'); expect(api.post).toHaveBeenCalledTimes(1); expect(api.post).toHaveBeenCalledWith('/skills/extract-ai')
    vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'Save temporarily unavailable' } } })
    await view.click('Save Selected'); expect(view.container.textContent).toContain('Save temporarily unavailable'); expect(view.container.textContent).toContain('Review Extracted Skills')
    expect(view.container.querySelector<HTMLInputElement>('input[value="C++"]')?.value).toBe('C++')
  } finally { await view.close() }
})
it('keeps the experience form and input when validation fails', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [] })
  const view = await mount(<ExperiencePage />)
  try {
    await view.click('Add Experience'); await input(view.container, 'Company', 'My company'); await input(view.container, 'Job Title', 'Engineer')
    vi.mocked(api.post).mockRejectedValueOnce({ response: { data: { detail: 'End date is before start date' } } })
    await view.click('Save'); expect(view.container.textContent).toContain('End date is before start date'); expect(view.container.querySelector<HTMLInputElement>('input[placeholder="Company"]')!.value).toBe('My company')
  } finally { await view.close() }
})

it('opens a complete portfolio preview without writing project data', async () => {
  vi.mocked(api.get).mockResolvedValue({ data: [{ id: 'p', name: 'Preview demo', isCurrent: true, isFeatured: false, description: 'Full description', technologies: 'React', readmePreview: 'Imported README' }] })
  const view = await mount(<PortfolioPage />)
  try {
    await view.click('Full preview')
    const dialog = document.querySelector('[role="dialog"]')!
    expect(dialog.textContent).toContain('Full description'); expect(dialog.textContent).toContain('Imported README'); expect(dialog.textContent).toContain('Present')
    expect(api.put).not.toHaveBeenCalled(); expect(api.post).not.toHaveBeenCalled()
    await act(async () => dialog.querySelector<HTMLButtonElement>('[aria-label="Close dialog"]')!.click())
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  } finally { await view.close() }
})
