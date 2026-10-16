// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { beforeEach, expect, it, vi } from 'vitest'
import TranscriptReview from './TranscriptReview'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { post: vi.fn() } }))
beforeEach(() => vi.resetAllMocks())
it('compares existing values, excludes duplicates by default, and saves only selected rows', async () => {
  vi.mocked(api.post).mockResolvedValue({ data: [{ index: 0, status: 'existing', existingCourse: { name: 'Saved course', grade: 'A', credits: 3 } }, { index: 1, status: 'new' }, { index: 2, status: 'batch_duplicate', duplicateOf: 1 }] })
  const onSave = vi.fn(), container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const data = { institution: { name: 'School' }, courses: [{ name: 'Existing', grade: 'B' }, { name: 'First' }, { name: 'Alternative' }] }
  try {
    await act(async () => root.render(<TranscriptReview data={data} saving={false} error={null} onSave={onSave} onCancel={vi.fn()} />))
    expect(onSave).not.toHaveBeenCalled(); expect(container.textContent).toContain('Already saved: Saved course'); expect(container.textContent).toContain('Grade: A'); expect(container.textContent).toContain('Grade: B')
    const boxes = container.querySelectorAll<HTMLInputElement>('input[type="checkbox"]'); expect(boxes[0].disabled).toBe(true); expect(boxes[1].checked).toBe(true); expect(boxes[2].checked).toBe(false)
    await act(async () => { boxes[1].click(); boxes[2].click() })
    await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Save to Education')!.click())
    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ unassigned: false, courses: [expect.objectContaining({ name: 'Alternative', updateExisting: false })] }))
  } finally { await act(async () => root.unmount()); container.remove() }
})
it('blocks save on preview failure and allows retry', async () => {
  vi.mocked(api.post).mockRejectedValueOnce(new Error('Offline')).mockResolvedValueOnce({ data: [] })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container), onSave = vi.fn()
  try {
    await act(async () => root.render(<TranscriptReview data={{ institution: { name: 'School' }, courses: [] }} saving={false} error={null} onSave={onSave} onCancel={vi.fn()} />))
    expect(container.textContent).toContain('Could not check'); expect(Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Save to Education')!.disabled).toBe(true); expect(onSave).not.toHaveBeenCalled()
    await act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Retry check')!.click()); expect(container.textContent).toContain('0 of 0 courses selected')
  } finally { await act(async () => root.unmount()); container.remove() }
})
it('requires explicit update approval and resets it when the destination changes', async () => {
  const existing = [{ index: 0, status: 'existing', existingCourse: { id: 'course', name: 'Saved', grade: 'A', credits: 3 }, fingerprint: 'revision' }]
  vi.mocked(api.post).mockResolvedValueOnce({ data: existing }).mockResolvedValueOnce({ data: [{ index: 0, status: 'new' }] }).mockResolvedValueOnce({ data: [{ index: 0, status: 'new' }] })
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container), onSave = vi.fn()
  const data = { institution: { name: 'Extracted' }, degree: { name: 'Extracted degree' }, courses: [{ name: 'Algorithms', grade: 'B', credits: '4' }] }
  const change = async (label: string, value: string) => act(async () => { const field = container.querySelector<HTMLSelectElement>(`[aria-label="${label}"]`)!; field.value = value; field.dispatchEvent(new Event('change', { bubbles: true })) })
  const save = async () => act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === 'Save to Education')!.click())
  try {
    await act(async () => root.render(<TranscriptReview data={data} institutions={[{ id: 'school', name: 'Selected school', degrees: [{ id: 'degree', degreeName: 'Selected degree' }] }]} saving={false} error={null} onSave={onSave} onCancel={vi.fn()} />))
    expect(container.querySelector<HTMLInputElement>('input[type="checkbox"]')!.checked).toBe(false); expect(onSave).not.toHaveBeenCalled()
    await act(async () => container.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click()); await save()
    expect(onSave).toHaveBeenLastCalledWith(expect.objectContaining({ courses: [expect.objectContaining({ updateExisting: true, existingCourseId: 'course', expectedFingerprint: 'revision', grade: 'B', credits: '4' })] }))
    await change('Destination institution', 'school'); expect(api.post).toHaveBeenLastCalledWith('/transcripts/preview', expect.objectContaining({ institutionId: 'school', degreeId: undefined, unassigned: false })); expect(container.textContent).toContain('0 updates approved')
    await change('Destination degree', 'degree'); await save(); expect(onSave).toHaveBeenLastCalledWith(expect.objectContaining({ institutionId: 'school', degreeId: 'degree', courses: [expect.objectContaining({ updateExisting: false })] }))
  } finally { await act(async () => root.unmount()); container.remove() }
})
