import { useEffect, useState } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'

interface Attempt { id: string; solvedAt: string; solutionLanguage?: string; timeSpentMinutes?: number; notes?: string; xpEarned: number }
interface Props { problemId: string; solved: boolean; attemptCount: number; onSaved: () => void }
const message = (error: any) => error?.response?.data?.detail || 'Could not save or load this attempt. Try again.'

export default function CodingAttempts({ problemId, solved, attemptCount, onSaved }: Props) {
  const [open, setOpen] = useState(false)
  const [attempts, setAttempts] = useState<Attempt[]>([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [language, setLanguage] = useState('')
  const [time, setTime] = useState('')
  const [notes, setNotes] = useState('')
  const load = async () => {
    setLoading(true); setError('')
    try { const { data } = await api.get(`/coding/problems/${problemId}/attempts`); setAttempts(data) }
    catch (error) { setError(message(error)) }
    finally { setLoading(false) }
  }
  useEffect(() => { if (open) void load() }, [open, problemId])
  const save = async (event: React.FormEvent) => {
    event.preventDefault()
    if (saving) return
    if (time && (!Number.isInteger(Number(time)) || Number(time) < 1 || Number(time) > 1440)) { setError('Time spent must be between 1 and 1440 whole minutes.'); return }
    setSaving(true); setError('')
    try {
      await api.post(`/coding/problems/${problemId}/${solved ? 'reattempt' : 'solve'}`, {
        solutionLanguage: language.trim() || undefined, timeSpentMinutes: time ? Number(time) : undefined, notes: notes.trim() || undefined,
      })
      setLanguage(''); setTime(''); setNotes(''); onSaved()
    } catch (error) { setError(message(error)) }
    finally { setSaving(false) }
  }
  return <section aria-label="Coding attempts" className="mt-3 space-y-3">
    <Button size="sm" variant="outline" onClick={() => setOpen(!open)} aria-expanded={open}>
      {open ? 'Close attempts' : `Attempts (${attemptCount}) / ${solved ? 'record review' : 'solve'}`}
    </Button>
    {open && <>
      <p className="text-xs text-muted-foreground">One entry per problem per local day. Repeated requests keep the existing entry and XP. Marking unsolved keeps your history.</p>
      <form onSubmit={save} className="space-y-2">
        <div className="flex flex-wrap gap-2">
          <input aria-label="Solution language" maxLength={100} placeholder="Language" className="px-3 py-2 border rounded bg-background text-sm" value={language} onChange={e => setLanguage(e.target.value)} />
          <input aria-label="Time spent in minutes" type="number" min={1} max={1440} step={1} placeholder="Minutes" className="w-28 px-3 py-2 border rounded bg-background text-sm" value={time} onChange={e => setTime(e.target.value)} />
        </div>
        <textarea aria-label="Approach and review notes" maxLength={5000} placeholder="Approach, complexity, mistakes and what to review next…" className="w-full px-3 py-2 border rounded bg-background text-sm" value={notes} onChange={e => setNotes(e.target.value)} />
        <Button size="sm" type="submit" disabled={saving || loading}>{saving ? 'Saving…' : solved ? 'Record review' : 'Mark solved'}</Button>
      </form>
      {loading ? <p role="status">Loading attempts…</p> : <ol className="space-y-2">
        {attempts.map(attempt => <li key={attempt.id} className="border rounded p-3 text-sm">
          <p>{new Date(attempt.solvedAt).toLocaleString()} · {attempt.xpEarned} XP{attempt.solutionLanguage ? ` · ${attempt.solutionLanguage}` : ''}{attempt.timeSpentMinutes ? ` · ${attempt.timeSpentMinutes} min` : ''}</p>
          {attempt.notes && <p className="mt-1 whitespace-pre-wrap text-muted-foreground">{attempt.notes}</p>}
        </li>)}
        {!attempts.length && <li className="text-sm text-muted-foreground">No attempts yet.</li>}
      </ol>}
    </>}
    {error && <div role="alert" className="text-sm text-destructive">{error} <button type="button" className="underline" onClick={load}>Reload history</button></div>}
  </section>
}
