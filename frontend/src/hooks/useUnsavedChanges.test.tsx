// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { afterEach, expect, it, vi } from 'vitest'
import { useUnsavedChanges, canLeavePage } from './useUnsavedChanges'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
function Form({ dirty }: { dirty: boolean }) { useUnsavedChanges(dirty); return <div>Draft</div> }
afterEach(() => vi.restoreAllMocks())
it('warns for dirty drafts and clears protection after save or unmount', async () => {
  const container = document.createElement('div')
  const root = createRoot(container)
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
  await act(async () => root.render(<Form dirty={false} />))
  expect(canLeavePage()).toBe(true)
  await act(async () => root.render(<Form dirty />))
  expect(canLeavePage()).toBe(false)
  expect(confirm).toHaveBeenCalledOnce()
  const event = new Event('beforeunload', { cancelable: true })
  window.dispatchEvent(event)
  expect(event.defaultPrevented).toBe(true)
  await act(async () => root.render(<Form dirty={false} />))
  expect(canLeavePage()).toBe(true)
  await act(async () => root.unmount())
  expect(canLeavePage()).toBe(true)
})
