import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import api from '@/lib/api'
import { dateKey, dateTimeInput, zonedInputToIso } from '@/lib/calendar-time'
import { Button } from '@/components/ui/button'
import { safeProjectUrl } from '../portfolio/PortfolioPreview'
interface Revision { round: string; format: string; status: string; startTime: string; endTime: string; interviewer?: string; location?: string; meetingUrl?: string; preparation?: string; outcome?: string; changedAt: string }
interface Interview extends Revision { id: string; updatedAt: string; calendarEventId?: string; history: Revision[] }
const fresh = () => ({ round: '', format: 'video', status: 'scheduled', start: '', end: '', interviewer: '', location: '', meetingUrl: '', preparation: '', outcome: '' })
const field = 'w-full border rounded-md p-2 bg-background text-sm'
export default function InterviewPanel({ applicationId, zone, onDirty, onBusy }: { applicationId: string; zone: string; onDirty: (value: boolean) => void; onBusy: (value: boolean) => void }) {
  const [items, setItems] = useState<Interview[]>([]), [loading, setLoading] = useState(true), [loadError, setLoadError] = useState(''), [error, setError] = useState(''), [busy, setBusy] = useState(false)
  const [draft, setDraft] = useState(fresh), [snapshot, setSnapshot] = useState(''), [editing, setEditing] = useState(false), [selected, setSelected] = useState<Interview | null>(null), [requestId, setRequestId] = useState(() => crypto.randomUUID()), [editZone, setEditZone] = useState(zone)
  const dirty = editing && JSON.stringify(draft) !== snapshot
  useEffect(() => { onDirty(dirty); return () => onDirty(false) }, [dirty, onDirty])
  useEffect(() => { onBusy(busy); return () => onBusy(false) }, [busy, onBusy])
  const path = `/job-applications/${applicationId}/interviews`
  const load = async () => { setLoading(true); setLoadError(''); try { setItems((await api.get<Interview[]>(path)).data) } catch { setLoadError('Could not load interviews.') } finally { setLoading(false) } }
  useEffect(() => { load() }, [applicationId])
  const edit = (item?: Interview) => {
    const value = item ? { round: item.round, format: item.format, status: item.status, start: dateTimeInput(item.startTime, zone), end: dateTimeInput(item.endTime, zone), interviewer: item.interviewer || '', location: item.location || '', meetingUrl: item.meetingUrl || '', preparation: item.preparation || '', outcome: item.outcome || '' } : fresh()
    setDraft(value); setSnapshot(JSON.stringify(value)); setSelected(item || null); setRequestId(crypto.randomUUID()); setEditZone(zone); setEditing(true); setError('')
  }
  const save = async () => {
    if (busy) return
    setError('')
    let body
    try { body = { ...draft, startTime: zonedInputToIso(draft.start, editZone), endTime: zonedInputToIso(draft.end, editZone), requestId, ...(selected ? { expectedUpdatedAt: selected.updatedAt } : {}) }; if (body.endTime <= body.startTime) throw Error('End time must be later than start time.') } catch (e: any) { setError(e.message); return }
    setBusy(true)
    try { if (selected) await api.put(path + '/' + selected.id, body); else await api.post(path, body); setEditing(false); await load(); window.dispatchEvent(new Event('notifications-updated')) } catch (e: any) { setError(e.response?.data?.detail || 'Could not save interview. Your edits are retained; try again.') } finally { setBusy(false) }
  }
  const summary = (item: Revision, currentZone: string) => <div className="space-y-1 text-sm"><p>{item.status} · {item.format} · {dateTimeInput(item.startTime, currentZone).replace('T', ' ')} – {dateTimeInput(item.endTime, currentZone).replace('T', ' ')}</p>{item.interviewer && <p>Interviewer: {item.interviewer}</p>}{item.location && <p>Location: {item.location}</p>}{safeProjectUrl(item.meetingUrl) && <a className="underline block" href={safeProjectUrl(item.meetingUrl)} target="_blank" rel="noopener noreferrer">Meeting link</a>}{item.preparation && <p className="whitespace-pre-wrap">Preparation: {item.preparation}</p>}{item.outcome && <p className="whitespace-pre-wrap">Outcome: {item.outcome}</p>}</div>
  return <section className="border rounded-lg p-3 space-y-3"><div className="flex items-center justify-between gap-2"><h3 className="font-medium">Interviews</h3><Button size="sm" variant="outline" disabled={busy || editing || loading || !!loadError} onClick={() => edit()}>Schedule interview</Button></div>
    <p className="text-xs text-muted-foreground">Times in {zone}. Interviews use the LifeOS local calendar. No invitations are sent; Google events are not created.</p>
    {loading && <p role="status">Loading interviews…</p>}{loadError && <p role="alert">{loadError} <button className="underline" onClick={load}>Try again</button></p>}
    {!loading && !loadError && !items.length && <p className="text-sm text-muted-foreground">No interview rounds recorded.</p>}
    {items.map(item => <article className="border rounded p-3 space-y-2 break-words" key={item.id}><h4 className="font-medium">{item.round}</h4>{summary(item, zone)}<div className="flex flex-wrap gap-3 text-sm"><Button size="sm" variant="outline" disabled={busy || editing} onClick={() => edit(item)}>Edit interview</Button>{item.calendarEventId && <Link className="underline" to={`/calendar?date=${dateKey(item.startTime, zone)}&event=${item.calendarEventId}`}>Calendar event</Link>}</div><details><summary className="cursor-pointer text-sm">Interview history ({item.history.length})</summary><ol className="space-y-3 mt-2">{item.history.map((h, i) => <li className="border-l-2 pl-3" key={i}><p className="text-xs text-muted-foreground">Saved {new Date(h.changedAt).toLocaleString('en-US', { timeZone: zone })} · {h.round}</p>{summary(h, zone)}</li>)}</ol></details></article>)}
    {editing && <form className="border-t pt-3 space-y-3" aria-label="Interview editor" onSubmit={e => { e.preventDefault(); save() }}>
      <h4 className="font-medium">{selected ? 'Edit interview' : 'New interview round'}</h4><p className="text-xs text-muted-foreground">Enter times in {editZone}. Cancellation removes the local calendar event and keeps history. To record a completed interview, choose Completed.</p>
      {error && <p role="alert" className="text-destructive">{error}</p>}
      <label className="block text-sm">Round<input required maxLength={150} className={field} value={draft.round} onChange={e => setDraft({ ...draft, round: e.target.value })} /></label>
      <div className="grid sm:grid-cols-2 gap-3"><label className="text-sm">Format<select className={field} value={draft.format} onChange={e => setDraft({ ...draft, format: e.target.value })}>{['phone', 'video', 'onsite', 'other'].map(s => <option key={s}>{s}</option>)}</select></label><label className="text-sm">Interview status<select className={field} value={draft.status} onChange={e => setDraft({ ...draft, status: e.target.value })}>{['scheduled', 'completed', 'cancelled'].map(s => <option key={s}>{s}</option>)}</select></label>
      <label className="text-sm">Interview start<input required type="datetime-local" className={field} value={draft.start} onChange={e => setDraft({ ...draft, start: e.target.value })} /></label><label className="text-sm">Interview end<input required type="datetime-local" className={field} value={draft.end} onChange={e => setDraft({ ...draft, end: e.target.value })} /></label></div>
      {(['interviewer', 'location', 'meetingUrl'] as const).map(key => <label key={key} className="block text-sm">{key === 'meetingUrl' ? 'Meeting URL' : key === 'interviewer' ? 'Interviewer' : 'Location'}<input className={field} type={key === 'meetingUrl' ? 'url' : 'text'} maxLength={key === 'meetingUrl' ? 2048 : key === 'location' ? 1000 : 300} value={draft[key]} onChange={e => setDraft({ ...draft, [key]: e.target.value })} /></label>)}
      {(['preparation', 'outcome'] as const).map(key => <label key={key} className="block text-sm">{key === 'preparation' ? 'Preparation notes' : 'Outcome and feedback'}<textarea maxLength={10000} className={field} value={draft[key]} onChange={e => setDraft({ ...draft, [key]: e.target.value })} /></label>)}
      <div className="flex gap-2"><Button disabled={busy}>{busy ? 'Saving…' : 'Save interview'}</Button><Button type="button" variant="outline" disabled={busy} onClick={() => { if (!dirty || window.confirm('Discard unsaved interview changes?')) setEditing(false) }}>Cancel editing</Button></div>
    </form>}
  </section>
}
