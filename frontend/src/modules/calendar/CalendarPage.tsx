import { useState, useEffect, useMemo, useRef } from 'react'
import api from '@/lib/api'
import { notify } from '@/lib/notify'
import { addDays, dateKey, dateTimeInput, zonedInputToIso } from '@/lib/calendar-time'
import { useUnsavedChanges } from '@/hooks/useUnsavedChanges'
import { Button } from '@/components/ui/button'
import { Modal } from '@/components/ui/modal'
import { Plus, ChevronLeft, ChevronRight, RefreshCw, ExternalLink } from 'lucide-react'
interface EventItem { applicationId?: string; id: string; title: string; description?: string; startTime: string; endTime?: string; isAllDay: boolean; startDate?: string; endDate?: string; location?: string; eventType: string; googleEventId?: string; calendarName?: string; editable?: boolean; etag?: string; htmlLink?: string }
interface Connection { configured: boolean; connected: boolean; accountEmail?: string; lastSyncAt?: string; error?: string; calendars: Array<{ id: string; name: string; selected: boolean; editable: boolean }>; shareDetailsWithCoach: boolean; redirectUri?: string }
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const field = 'w-full px-3 py-2 rounded-md border bg-background text-sm'
const empty = { title: '', description: '', start: '', end: '', isAllDay: false, location: '', eventType: 'general', destination: 'local' }
export default function CalendarPage() {
  const [zone, setZone] = useState('America/Los_Angeles')
  const [anchor, setAnchor] = useState(new URLSearchParams(window.location.search).get('date')?.match(/^\d{4}-\d{2}-\d{2}$/)?.[0] || dateKey(new Date(), 'America/Los_Angeles'))
  const [mode, setMode] = useState<'month' | 'week' | 'agenda'>('month')
  const [events, setEvents] = useState<EventItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [connection, setConnection] = useState<Connection | null>(null)
  const [selectedCalendars, setSelectedCalendars] = useState<string[]>([])
  const [shareDetails, setShareDetails] = useState(false)
  const [showSettings, setShowSettings] = useState(false)
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<EventItem | null>(null)
  const [form, setForm] = useState(empty)
  const [snapshot, setSnapshot] = useState('')
  const [saving, setSaving] = useState(false)
  const dirty = open && JSON.stringify(form) !== snapshot
  const settingsDirty = !!connection?.connected && (JSON.stringify([...selectedCalendars].sort()) !== JSON.stringify(connection.calendars.filter(c => c.selected).map(c => c.id).sort()) || shareDetails !== connection.shareDetailsWithCoach)
  useUnsavedChanges(dirty || settingsDirty)
  const latestRequest = useRef(0)
  const openedLink = useRef(false)
  const readonly = !!editing?.googleEventId && !editing.editable
  const range = useMemo(() => {
    const first = anchor.slice(0, 7) + '-01'
    const start = mode === 'week' ? addDays(anchor, -new Date(anchor + 'T12:00:00Z').getUTCDay()) : mode === 'month' ? addDays(first, -new Date(first + 'T12:00:00Z').getUTCDay()) : first
    const nextMonth = new Date(first + 'T12:00:00Z'); nextMonth.setUTCMonth(nextMonth.getUTCMonth() + 1)
    const end = mode === 'week' ? addDays(start, 7) : mode === 'month' ? addDays(start, 42) : nextMonth.toISOString().slice(0, 10)
    return { start, end }
  }, [anchor, mode])
  const days = useMemo(() => { const result: string[] = []; for (let day = range.start; day < range.end; day = addDays(day, 1)) result.push(day); return result }, [range])
  const reloadStatus = async (resetSelection = true) => {
    const { data } = await api.get('/googlecalendar/status'); setConnection(data)
    if (resetSelection) { setSelectedCalendars(data.calendars.filter((c: { selected: boolean }) => c.selected).map((c: { id: string }) => c.id)); setShareDetails(data.shareDetailsWithCoach) }
  }
  useEffect(() => {
    api.get('/profile').then(({ data }) => { const zone = data.timeZone || 'America/Los_Angeles'; try { if (!new URLSearchParams(window.location.search).has('date')) setAnchor(dateKey(new Date(), zone)); setZone(zone) } catch { /* Use the default timezone for invalid profiles. */ } }).catch(() => {})
    reloadStatus().catch(() => notify('Could not load the Google Calendar connection.', 'error'))
    const result = new URLSearchParams(window.location.search).get('google')
    if (result) { notify(result === 'connected' ? 'Google Calendar connected.' : result === 'cancelled' ? 'Google connection cancelled.' : 'Google connection failed. Check setup and try again.', result === 'failed' ? 'error' : 'info'); window.history.replaceState({}, '', window.location.pathname) }
  }, [])
  const load = async (refresh = false, signal?: AbortSignal) => {
    const sequence = ++latestRequest.current
    setLoading(true); setError('')
    try {
      const params = { start: zonedInputToIso(range.start + 'T00:00', zone), end: zonedInputToIso(range.end + 'T00:00', zone) }
      const results = await Promise.allSettled([api.get('/calendar/events', { params, signal }), api.get('/googlecalendar/events', { params: { ...params, refresh }, signal })])
      if (signal?.aborted || sequence !== latestRequest.current) return
      const combined: EventItem[] = []
      for (let i = 0; i < results.length; i++) {
        const result = results[i]
        if (result.status === 'fulfilled') combined.push(...result.value.data)
        else setError(i === 0 ? 'Could not load LifeOS events. Try again.' : 'Google events could not be refreshed. Your LifeOS events are still available.')
      }
      setEvents(combined.sort((a, b) => Date.parse(a.startTime) - Date.parse(b.startTime)))
      if (refresh) await reloadStatus(false)
    } catch (err: any) { if (!signal?.aborted && sequence === latestRequest.current) setError(err.message || 'Could not load events.') }
    finally { if (!signal?.aborted && sequence === latestRequest.current) setLoading(false) }
  }
  useEffect(() => { const controller = new AbortController(); load(false, controller.signal); return () => controller.abort() }, [range.start, range.end, zone])
  const openNew = (day: string) => {
    const next = { ...empty, start: day + 'T09:00', end: day + 'T10:00', destination: connection?.connected ? 'google' : 'local' }
    setEditing(null); setForm(next); setSnapshot(JSON.stringify(next)); setOpen(true)
  }
  const openEvent = (event: EventItem) => {
    const start = event.isAllDay ? event.startDate || dateKey(event.startTime, zone) : dateTimeInput(event.startTime, zone)
    const end = event.isAllDay ? event.endDate ? addDays(event.endDate, -1) : event.endTime ? addDays(dateKey(event.endTime, zone), -1) : start : event.endTime ? dateTimeInput(event.endTime, zone) : start
    const next = { title: event.title, description: event.description || '', location: event.location || '', isAllDay: event.isAllDay, start, end, eventType: event.eventType || 'general', destination: event.googleEventId ? 'google' : 'local' }
    setEditing(event); setForm(next); setSnapshot(JSON.stringify(next)); setOpen(true)
  }
  useEffect(() => { const id = new URLSearchParams(window.location.search).get('event'); const event = events.find(e => e.id === id || e.googleEventId === id); if (event && !openedLink.current) { openedLink.current = true; openEvent(event) } }, [events, zone])
  const close = () => { if (!dirty || window.confirm('Discard unsaved event changes?')) setOpen(false) }
  const save = async () => {
    if (readonly) return
    try {
      if (!form.title.trim()) throw new Error('Enter an event title.')
      if (!form.start || !form.end) throw new Error('Enter start and end dates.')
      const start = form.isAllDay ? form.start + 'T00:00' : form.start
      const end = form.isAllDay ? addDays(form.end, 1) + 'T00:00' : form.end
      const startTime = zonedInputToIso(start, zone), endTime = zonedInputToIso(end, zone)
      if (endTime <= startTime) throw new Error('The end must be later than the start.')
      setSaving(true)
      const payload = { title: form.title, description: form.description, location: form.location, eventType: form.eventType, isAllDay: form.isAllDay, startTime, endTime, startDate: form.isAllDay ? form.start : undefined, endDate: form.isAllDay ? addDays(form.end, 1) : undefined, etag: editing?.etag }
      const path = form.destination === 'google' ? '/googlecalendar/events' : '/calendar/events'
      if (editing) await api.put(path + '/' + encodeURIComponent(editing.googleEventId || editing.id), payload)
      else await api.post(path, payload)
      setSnapshot(JSON.stringify(form)); setOpen(false); notify('Event saved.', 'success'); await load(true)
    } catch (err: any) { notify(err.response?.data?.detail || err.message || 'Could not save event.', 'error') }
    finally { setSaving(false) }
  }
  const remove = async () => {
    if (!editing || readonly || !window.confirm('Delete this event?')) return
    setSaving(true)
    try { await api.delete((editing.googleEventId ? '/googlecalendar/events/' : '/calendar/events/') + encodeURIComponent(editing.googleEventId || editing.id), { params: editing.googleEventId ? { etag: editing.etag } : undefined }); setOpen(false); notify('Event deleted.', 'success'); await load(true) }
    catch (err: any) { notify(err.response?.data?.detail || 'Could not delete event.', 'error') }
    finally { setSaving(false) }
  }
  const onDay = (day: string) => events.filter(event => {
    if (event.isAllDay && event.startDate) return event.startDate <= day && (event.endDate || addDays(event.startDate, 1)) > day
    const start = zonedInputToIso(day + 'T00:00', zone), end = zonedInputToIso(addDays(day, 1) + 'T00:00', zone)
    return Date.parse(event.startTime) < Date.parse(end) && (event.endTime ? Date.parse(event.endTime) > Date.parse(start) : Date.parse(event.startTime) >= Date.parse(start))
  })
  const shift = (direction: number) => {
    if (mode === 'week') setAnchor(addDays(anchor, direction * 7))
    else { const value = new Date(anchor.slice(0, 7) + '-01T12:00:00Z'); value.setUTCMonth(value.getUTCMonth() + direction); setAnchor(value.toISOString().slice(0, 10)) }
  }
  const connect = async () => { try { const { data } = await api.post('/googlecalendar/connect'); window.location.assign(data.url) } catch (err: any) { notify(err.response?.data?.detail || 'Could not start Google sign-in.', 'error') } }
  const disconnect = async () => { if (!window.confirm('Disconnect Google Calendar? Events stored in Google will be kept.')) return; try { await api.post('/googlecalendar/disconnect'); await reloadStatus(); await load(); notify('Google Calendar disconnected.', 'success') } catch (err: any) { notify(err.response?.data?.detail || 'Could not disconnect.', 'error') } }
  const settings = async () => { try { await api.put('/googlecalendar/settings', { calendarIds: selectedCalendars, shareDetailsWithCoach: shareDetails }); await reloadStatus(); await load(true); notify('Calendar selection saved.', 'success') } catch (err: any) { notify(err.response?.data?.detail || 'Could not save calendar selection.', 'error') } }
  const label = (day: string) => new Date(day + 'T12:00:00Z').toLocaleDateString('en-US', { timeZone: 'UTC', weekday: 'short', month: 'short', day: 'numeric' })
  return <div className="max-w-6xl mx-auto space-y-4">
    <header className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-bold">Calendar</h1><p className="text-sm text-muted-foreground">Interviews, follow-ups, and time for your goals · {zone}</p></div><div className="flex gap-2"><Button variant="outline" onClick={() => setShowSettings(!showSettings)}>Google Calendar</Button><Button onClick={() => openNew(dateKey(new Date(), zone))}><Plus size={16} className="mr-2" />Add Event</Button></div></header>
    {showSettings && <section className="bg-card border rounded-xl p-4 space-y-3" aria-label="Google Calendar connection">
      <h2 className="font-semibold">Google Calendar</h2>
      {!connection?.configured ? <p className="text-sm text-muted-foreground">Google Calendar needs one-time setup by the server administrator. Your local calendar works while setup is pending.</p> : !connection.connected ? <><p className="text-sm text-muted-foreground">Connect to display your calendars and edit events in a dedicated LifeOS calendar. Existing calendars stay read-only here.</p><Button onClick={connect}>Connect Google Calendar</Button></> : <>
        <p className="text-sm">Connected as {connection.accountEmail || 'your Google account'}. {connection.lastSyncAt ? 'Last synced ' + new Date(connection.lastSyncAt).toLocaleString() : 'Ready to synchronize.'}</p>
        {connection.error && <p role="alert" className="text-sm text-red-400">{connection.error}</p>}
        <div className="grid sm:grid-cols-2 gap-2">{connection.calendars.map(calendar => <label key={calendar.id} className="flex gap-2 items-center text-sm"><input type="checkbox" checked={selectedCalendars.includes(calendar.id)} onChange={e => setSelectedCalendars(old => e.target.checked ? [...old, calendar.id] : old.filter(id => id !== calendar.id))} />{calendar.name} <span className="text-xs text-muted-foreground">{calendar.editable ? 'editable' : 'read-only'}</span></label>)}</div>
        <label className="flex gap-2 items-center text-sm"><input type="checkbox" checked={shareDetails} onChange={e => setShareDetails(e.target.checked)} />Share Google event details with AI Coach; otherwise share busy times only.</label>
        <div className="flex flex-wrap gap-2"><Button onClick={settings}>Save Selection</Button><Button variant="outline" onClick={() => load(true)}>Sync Now</Button><Button variant="outline" onClick={connect}>Reconnect</Button><Button variant="outline" onClick={disconnect}>Disconnect</Button></div>
      </>}
    </section>}
    <div className="flex flex-wrap items-center justify-between gap-3"><div className="flex gap-1">{(['month', 'week', 'agenda'] as const).map(view => <Button size="sm" key={view} variant={mode === view ? 'default' : 'outline'} onClick={() => setMode(view)}>{view[0].toUpperCase() + view.slice(1)}</Button>)}</div><div className="flex items-center gap-2"><Button size="sm" variant="outline" aria-label="Previous period" onClick={() => shift(-1)}><ChevronLeft size={16} /></Button><span className="text-sm font-medium">{mode === 'week' ? label(range.start) : new Date(anchor + 'T12:00:00Z').toLocaleDateString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' })}</span><Button size="sm" variant="outline" aria-label="Next period" onClick={() => shift(1)}><ChevronRight size={16} /></Button><Button size="sm" variant="outline" onClick={() => setAnchor(dateKey(new Date(), zone))}>Today</Button><Button size="sm" variant="outline" aria-label="Refresh calendar" disabled={loading} onClick={() => load(true)}><RefreshCw size={16} className={loading ? 'animate-spin' : ''} /></Button></div></div>
    {error && <div role="alert" className="border border-red-400/30 rounded-lg p-3 text-sm">{error} <button className="underline" onClick={() => load(true)}>Retry</button></div>}
    {loading && <p role="status" className="text-sm text-muted-foreground">Refreshing events…</p>}
    {mode === 'agenda' ? <div className="space-y-3">{days.filter(day => onDay(day).length > 0).map(day => <section key={day} className="bg-card border rounded-xl p-4"><h2 className="font-medium mb-2">{label(day)}</h2>{onDay(day).map(event => <button key={event.id} className="w-full text-left border-t py-3" onClick={() => openEvent(event)}><span className="font-medium">{event.title}</span><span className="block text-xs text-muted-foreground">{event.isAllDay ? 'All day' : dateTimeInput(event.startTime, zone).slice(11)} · {event.calendarName || 'LifeOS local'}</span></button>)}</section>)}{events.length === 0 && <p className="text-muted-foreground p-6 text-center">No events this month. Add an interview, follow-up, or study session.</p>}</div> : <div className="overflow-x-auto"><div className="min-w-[650px] rounded-xl border overflow-hidden"><div className="grid grid-cols-7 bg-muted">{DAYS.map(day => <div key={day} className="p-2 text-center text-xs font-medium">{day}</div>)}</div><div className="grid grid-cols-7">{days.map(day => <div key={day} className={`min-h-[115px] border-r border-b p-2 ${day === dateKey(new Date(), zone) ? 'bg-primary/10' : day.slice(0, 7) !== anchor.slice(0, 7) && mode === 'month' ? 'opacity-60' : ''}`}><button className="text-xs font-semibold mb-2 hover:underline" aria-label={'Add event on ' + label(day)} onClick={() => openNew(day)}>{Number(day.slice(-2))}</button><div className="space-y-1">{onDay(day).map(event => <button key={event.id} title={event.title} onClick={() => openEvent(event)} className={`w-full text-left text-xs rounded px-2 py-1 truncate ${event.googleEventId ? 'bg-purple-500/20 text-purple-200' : 'bg-blue-500/20 text-blue-200'}`}>{!event.isAllDay && dateTimeInput(event.startTime, zone).slice(11) + ' '}{event.title}</button>)}</div></div>)}</div></div></div>}
    <Modal open={open} onClose={close} title={editing ? readonly ? 'Google Event' : 'Edit Event' : 'New Event'}>
      <div className="space-y-3"><p className="text-xs text-muted-foreground">Times are shown in {zone}.{readonly && ' This calendar is read-only in LifeOS.'}</p>
        <label className="block text-sm">Title<input className={field} value={form.title} disabled={readonly || saving} maxLength={200} onChange={e => setForm({ ...form, title: e.target.value })} /></label>
        <label className="block text-sm">Description<textarea className={field} value={form.description} disabled={readonly || saving} rows={3} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
        <label className="flex gap-2 text-sm"><input type="checkbox" checked={form.isAllDay} disabled={readonly || saving} onChange={e => setForm({ ...form, isAllDay: e.target.checked, start: e.target.checked ? form.start.slice(0, 10) : form.start + 'T09:00', end: e.target.checked ? form.end.slice(0, 10) : form.end + 'T10:00' })} />All day</label>
        {editing?.applicationId && <a className="text-sm underline" href={'/jobs/applications?application=' + editing.applicationId}>Open linked application</a>}
        <div className="grid sm:grid-cols-2 gap-3"><label className="text-sm">Start<input className={field} type={form.isAllDay ? 'date' : 'datetime-local'} value={form.start} disabled={readonly || saving} onChange={e => setForm({ ...form, start: e.target.value })} /></label><label className="text-sm">{form.isAllDay ? 'Last day' : 'End'}<input className={field} type={form.isAllDay ? 'date' : 'datetime-local'} value={form.end} disabled={readonly || saving} onChange={e => setForm({ ...form, end: e.target.value })} /></label></div>
        <label className="block text-sm">Location<input className={field} value={form.location} disabled={readonly || saving} onChange={e => setForm({ ...form, location: e.target.value })} /></label>
        {!editing && <label className="block text-sm">Calendar<select className={field} value={form.destination} onChange={e => setForm({ ...form, destination: e.target.value })}><option value="local">LifeOS local</option>{connection?.connected && <option value="google">LifeOS in Google Calendar</option>}</select></label>}
        {form.destination === 'local' && <label className="block text-sm">Type<select className={field} value={form.eventType} disabled={saving} onChange={e => setForm({ ...form, eventType: e.target.value })}>{['general', 'job', 'coding', 'habit', 'meeting'].map(type => <option key={type}>{type}</option>)}</select></label>}
        {editing?.htmlLink?.startsWith('https://') && <a className="text-sm underline inline-flex gap-1 items-center" href={editing.htmlLink} target="_blank" rel="noopener noreferrer">Open in Google Calendar<ExternalLink size={14} /></a>}
        {editing?.googleEventId && <p className="text-xs text-muted-foreground">Changes apply to this event occurrence. If Google has a newer version, LifeOS will ask you to reload before saving.</p>}
        <div className="flex justify-between gap-2 pt-3">{editing && !readonly ? <Button variant="outline" onClick={remove} disabled={saving}>Delete</Button> : <span />}<div className="flex gap-2"><Button variant="outline" onClick={close} disabled={saving}>Close</Button>{!readonly && <Button onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>}</div></div>
      </div>
    </Modal>
  </div>
}
