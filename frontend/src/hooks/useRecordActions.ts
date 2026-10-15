import { useRef, useState } from 'react'
export function recordError(error: any) {
  const data = error?.response?.data
  if (data?.detail) return data.detail as string
  if (data?.errors) return Object.values(data.errors).flat().join(' ')
  return 'Could not update these records. Your form is still open; try again.'
}
export function useRecordActions() {
  const [busy, setBusy] = useState(false), [error, setError] = useState('')
  const pending = useRef(false)
  const mutate = async (action: () => Promise<void>) => {
    if (pending.current) return
    pending.current = true; setBusy(true); setError('')
    try { await action() } catch (error) { setError(recordError(error)) }
    finally { pending.current = false; setBusy(false) }
  }
  return { busy, error, setError, mutate }
}
