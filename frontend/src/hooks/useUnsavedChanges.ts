import { useEffect, useId } from 'react'

const dirtyForms = new Set<string>()
export function canLeavePage() {
  return dirtyForms.size === 0 || window.confirm('You have unsaved changes. Leave this page?')
}
export function useUnsavedChanges(dirty: boolean) {
  const id = useId()
  useEffect(() => {
    if (dirty) dirtyForms.add(id)
    else dirtyForms.delete(id)
    const warn = (event: BeforeUnloadEvent) => {
      if (dirtyForms.size) { event.preventDefault(); event.returnValue = '' }
    }
    window.addEventListener('beforeunload', warn)
    return () => { dirtyForms.delete(id); window.removeEventListener('beforeunload', warn) }
  }, [dirty, id])
}
