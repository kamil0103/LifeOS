import { useEffect, useRef, useState } from 'react'
import api from '@/lib/api'
import { notify } from '@/lib/notify'
import { Button } from '@/components/ui/button'
import { Modal } from '@/components/ui/modal'
interface Habit { id: string; name: string; category: string; targetValue?: number; unit?: string; icon?: string; color?: string; scheduleDaysMask: number; isActive: boolean; isDueToday: boolean; isCompleted: boolean; todayValue: number; currentStreak: number; longestStreak: number; updatedAt: string; timeZone: string }
interface History { id: string; completedAt: string; value?: number; notes?: string; isCompleted: boolean }
const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const categories = ['Coding', 'Exercise', 'Bible', 'Prayer', 'Reading', 'Applications', 'Sleep', 'Water', 'Other']
const blank = { id: '', name: '', category: 'Coding', targetValue: '', unit: '', icon: '', color: '', scheduleDaysMask: 127, updatedAt: '' }
const field = 'w-full p-2 border rounded-md bg-background'
export default function HabitsPage() {
  const loadSequence = useRef(0)
  const [habits, setHabits] = useState<Habit[]>([]), [view, setView] = useState('today')
  const [loading, setLoading] = useState(true), [error, setError] = useState(''), [pending, setPending] = useState('')
  const [form, setForm] = useState<typeof blank | null>(null), [formError, setFormError] = useState('')
  const [archive, setArchive] = useState<Habit | null>(null), [totals, setTotals] = useState<Record<string, string>>({})
  const [history, setHistory] = useState<Habit | null>(null), [records, setRecords] = useState<History[]>([]), [historyError, setHistoryError] = useState(''), [historyLoading, setHistoryLoading] = useState(false)
  const fail = (e: any) => e.response?.data?.detail || 'Could not save. Please try again.'
  const load = async () => {
    const sequence = ++loadSequence.current
    setLoading(true); setError('')
    try { const { data } = await api.get('/habits', { params: { archived: view === 'archived' } }); if (sequence === loadSequence.current) setHabits(data) }
    catch { if (sequence === loadSequence.current) setError('Could not load habits. Try again.') } finally { if (sequence === loadSequence.current) setLoading(false) }
  }
  useEffect(() => { load() }, [view])
  const edit = (habit?: Habit) => { setFormError(''); setForm(habit ? { ...blank, ...habit, targetValue: habit.targetValue == null ? '' : String(habit.targetValue), unit: habit.unit || '', icon: habit.icon || '', color: habit.color || '' } : { ...blank }) }
  const save = async () => {
    if (!form || pending) return
    setPending('form'); setFormError('')
    try {
      const payload = { ...form, targetValue: form.targetValue === '' ? null : Number(form.targetValue), expectedUpdatedAt: form.id ? form.updatedAt : undefined, isActive: true }
      if (form.id) await api.put('/habits/' + form.id, payload); else await api.post('/habits', payload)
      setForm(null); await load()
    } catch (e) { setFormError(fail(e)) } finally { setPending('') }
  }
  const progress = async (habit: Habit) => {
    if (pending) return
    setPending(habit.id); setError('')
    try {
      const numeric = habit.targetValue != null
      const input = totals[habit.id] ?? String(habit.todayValue)
      if (numeric && (!input.trim() || !Number.isFinite(Number(input)))) { setError('Enter a valid total progress value.'); return }
      const { data } = numeric ? await api.post(`/habits/${habit.id}/progress`, { value: Number(input) }) : await api.post(`/habits/${habit.id}/complete`)
      if (data.xpEarned > 0) notify(`+${data.xpEarned} XP for ${habit.name}`)
      await load()
    } catch (e) { setError(fail(e)) } finally { setPending('') }
  }
  const setActive = async (habit: Habit, active: boolean) => {
    if (pending) return
    setPending(habit.id); setError('')
    try { if (active) await api.post(`/habits/${habit.id}/restore`); else await api.delete(`/habits/${habit.id}`); setArchive(null); await load() }
    catch (e) { setError(fail(e)) } finally { setPending('') }
  }
  const loadHistory = async (habit: Habit) => {
    setHistory(habit); setHistoryLoading(true); setHistoryError(''); setRecords([])
    try { setRecords((await api.get(`/habits/${habit.id}/completions`, { params: { days: 30 } })).data) }
    catch { setHistoryError('Could not load history. Try again.') } finally { setHistoryLoading(false) }
  }
  const visible = habits.filter(h => view !== 'today' || h.isDueToday)
  return <div className="p-4 md:p-8 max-w-5xl mx-auto space-y-5">
    <header className="flex flex-wrap justify-between gap-3"><div><h1 className="text-3xl font-bold">Habits</h1><p className="text-muted-foreground">Build consistency on your schedule. Numeric progress earns XP when the target is reached.</p></div><Button onClick={() => edit()}>Add Habit</Button></header>
    <div className="flex gap-2 flex-wrap" aria-label="Habit views">{[['today', 'Today'], ['all', 'All active'], ['archived', 'Archived']].map(([key, label]) => <Button key={key} aria-pressed={view === key} variant={view === key ? 'default' : 'outline'} disabled={!!pending} onClick={() => setView(key)}>{label}</Button>)}</div>
    {error && <div role="alert" className="text-red-400">{error} <Button variant="outline" onClick={load}>Reload habits</Button></div>}
    {loading && <p role="status">Loading habits…</p>}
    {!loading && !visible.length && <p className="text-muted-foreground">{view === 'today' ? 'No habits scheduled today. Use All active to manage your habits.' : view === 'archived' ? 'No archived habits.' : 'No active habits. Add a habit to begin.'}</p>}
    <div className="grid gap-4 md:grid-cols-2">{!loading && visible.map(h => <article key={h.id} className="border rounded-xl p-4 space-y-3 bg-card">
      <div><h2 className="font-semibold">{h.name}</h2><p className="text-sm text-muted-foreground">{h.category} · {h.scheduleDaysMask === 127 ? 'Every day' : days.filter((_, i) => h.scheduleDaysMask & (1 << i)).join(', ')}</p><p className="text-xs text-muted-foreground">{h.timeZone} · {h.currentStreak} scheduled-day streak · Best {h.longestStreak}</p></div>
      {!h.isDueToday && h.isActive && <p className="text-sm text-muted-foreground">Not scheduled today</p>}
      {h.isCompleted && <p className="text-green-400">Completed today</p>}
      {h.targetValue != null && <div><label className="block text-sm">Today's total for {h.name} ({h.unit || 'units'})<input aria-label={`Today's total for ${h.name}`} className={field} type="number" min="0" max="1000000" step="any" value={totals[h.id] ?? String(h.todayValue)} onChange={e => setTotals(current => ({ ...current, [h.id]: e.target.value }))} disabled={!h.isDueToday || !h.isActive || !!pending} /></label><progress className="w-full" aria-label={`Progress for ${h.name}`} value={Math.min(h.todayValue, h.targetValue)} max={h.targetValue} /><p className="text-sm text-muted-foreground">Saved: {h.todayValue} / {h.targetValue} {h.unit}. Enter the total, not an amount to add.</p></div>}
      <div className="flex flex-wrap gap-2">{h.isActive && h.isDueToday && (h.targetValue != null || !h.isCompleted) && <Button disabled={!!pending} onClick={() => progress(h)}>{pending === h.id ? 'Saving…' : h.targetValue != null ? 'Save progress' : 'Complete'}</Button>}{h.isActive && <Button variant="outline" disabled={!!pending} onClick={() => edit(h)}>Edit {h.name}</Button>}<Button variant="outline" onClick={() => loadHistory(h)}>History</Button>{h.isActive ? <Button variant="outline" disabled={!!pending} onClick={() => setArchive(h)}>Archive</Button> : <Button disabled={!!pending} onClick={() => setActive(h, true)}>Restore {h.name}</Button>}</div>
    </article>)}</div>
    <Modal open={!!form} onClose={() => { if (!pending) setForm(null) }} title={form?.id ? 'Edit Habit' : 'Add Habit'}>{form && <form onSubmit={e => { e.preventDefault(); save() }} className="space-y-3">
      <fieldset disabled={!!pending} className="space-y-3"><label className="block">Name<input className={field} required maxLength={200} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label><label className="block">Category<select className={field} value={form.category} onChange={e => setForm({ ...form, category: e.target.value })}>{Array.from(new Set([...categories, form.category])).map(c => <option key={c}>{c}</option>)}</select></label>
      <label className="block">Numeric target (optional)<input className={field} type="number" min="0.001" max="1000000" step="any" value={form.targetValue} onChange={e => setForm({ ...form, targetValue: e.target.value })} /></label><label className="block">Unit<input className={field} maxLength={50} placeholder="minutes, pages, glasses" value={form.unit} onChange={e => setForm({ ...form, unit: e.target.value })} /></label>
      <fieldset><legend>Scheduled days</legend><p className="text-sm text-muted-foreground">Choose at least one day. Schedules use your profile timezone.</p><div className="flex gap-3 flex-wrap mt-2">{days.map((day, i) => <label key={day}><input type="checkbox" checked={!!(form.scheduleDaysMask & (1 << i))} onChange={() => setForm({ ...form, scheduleDaysMask: form.scheduleDaysMask ^ (1 << i) })} /> {day}</label>)}</div><div className="flex flex-wrap gap-2 mt-2">{[[127, 'Every day'], [62, 'Weekdays'], [65, 'Weekends']].map(([mask, label]) => <Button key={mask} type="button" variant="outline" onClick={() => setForm({ ...form, scheduleDaysMask: Number(mask) })}>{label}</Button>)}</div></fieldset>
      {form.id && <p className="text-xs text-muted-foreground">Changes affect future scheduling and current targets. Recorded achievements and XP are preserved.</p>}</fieldset>
      {formError && <p role="alert" className="text-red-400">{formError}</p>}<Button type="submit" disabled={!!pending || !form.scheduleDaysMask}>{pending === 'form' ? 'Saving…' : 'Save habit'}</Button>
    </form>}</Modal>
    <Modal open={!!archive} onClose={() => { if (!pending) setArchive(null) }} title="Archive habit"><p>{archive?.name} will leave your active list. Its history and XP are kept, and you can restore it from Archived.</p>{error && <p role="alert" className="text-red-400">{error}</p>}<Button className="mt-4" disabled={!!pending} onClick={() => archive && setActive(archive, false)}>Archive habit</Button></Modal>
    <Modal open={!!history} onClose={() => setHistory(null)} title={`History: ${history?.name || ''}`}><p className="text-sm text-muted-foreground">Last 30 days · {history?.timeZone}</p>{historyLoading && <p role="status">Loading history…</p>}{historyError && <p role="alert">{historyError} <Button onClick={() => history && loadHistory(history)}>Retry history</Button></p>}{!historyLoading && !historyError && !records.length && <p>No progress recorded.</p>}<ul className="space-y-2 mt-3">{records.map(r => <li key={r.id} className="border rounded p-2">{new Date(r.completedAt).toLocaleDateString(undefined, { timeZone: history?.timeZone })} · {r.isCompleted ? 'Completed' : 'Partial progress'}{r.value != null && ` · ${r.value} ${history?.unit || ''}`}{r.notes && <p>{r.notes}</p>}</li>)}</ul></Modal>
  </div>
}
