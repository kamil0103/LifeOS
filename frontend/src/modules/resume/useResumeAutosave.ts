import { useCallback, useEffect, useRef, useState } from 'react'
import api from '@/lib/api'
export function useResumeAutosave(data: unknown) {
  const state = useRef({ initialized: false, revision: null as string | null, desired: '', saved: '', running: false, paused: false, alive: true, timer: undefined as ReturnType<typeof setTimeout> | undefined })
  const [status, setStatus] = useState('Loading server draft…'), [error, setError] = useState(''), [conflict, setConflict] = useState(false)
  const flush = useCallback(async function save() {
    const s = state.current
    if (!s.alive || !s.initialized || s.running || s.paused || s.desired === s.saved) return
    s.running = true; const payload = s.desired; setStatus('Saving draft…'); setError('')
    try {
      const { data: result } = await api.put('/resume-draft', { data: JSON.parse(payload), expectedRevision: s.revision })
      s.revision = result.revision; s.saved = payload
      if (s.alive) setStatus(s.desired === s.saved ? 'Draft saved to server' : 'Autosave pending…')
    } catch (e: any) {
      s.paused = true
      if (s.alive) { setConflict(e.response?.status === 409); setStatus('Draft not saved'); setError(e.response?.data?.detail || 'Autosave failed. Your local edits are retained; retry saving.') }
    } finally {
      s.running = false
      if (s.alive && !s.paused && s.desired !== s.saved) { clearTimeout(s.timer); s.timer = setTimeout(save, 1000) }
    }
  }, [])
  const initialize = useCallback((value: unknown, revision: string | null) => { const s = state.current; s.initialized = true; s.revision = revision; s.desired = s.saved = JSON.stringify(value); s.paused = false; setError(''); setConflict(false); setStatus(revision ? 'Server draft restored' : 'Draft ready · changes autosave to server') }, [])
  useEffect(() => {
    const s = state.current; if (!data || !s.initialized) return
    s.desired = JSON.stringify(data); clearTimeout(s.timer)
    if (s.desired !== s.saved && !s.paused) { if (!s.running) setStatus('Autosave pending…'); s.timer = setTimeout(flush, 1000) }
    return () => clearTimeout(s.timer)
  }, [data, flush])
  useEffect(() => { state.current.alive = true; return () => { state.current.alive = false; clearTimeout(state.current.timer) } }, [])
  const retry = () => { if (conflict) return; state.current.paused = false; setError(''); flush() }
  return { status, error, conflict, initialize, retry }
}
