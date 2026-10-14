import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import api from '@/lib/api'
import { notify } from '@/lib/notify'
import { dateTimeInput, zonedInputToIso } from '@/lib/calendar-time'
import { Button } from '@/components/ui/button'
import { Modal } from '@/components/ui/modal'
export interface DailyAction { id: string; kind: string; sourceId?: string; title: string; reason: string; priority: number; completed: boolean; dueAt?: string; actionUrl: string; canComplete: boolean }
interface Plan { date: string; timeZone: string; actions: DailyAction[]; commitments: { id: string; title: string; startTime: string; endTime?: string; isAllDay: boolean; source: string; actionUrl: string }[]; googleWarning?: string }
const field = 'w-full border rounded-md p-2 bg-background'
export default function DailyPlan({ onCompleted }: { onCompleted?: () => void }) {
  const queryClient = useQueryClient(), navigate = useNavigate()
  const plan = useQuery({ queryKey: ['daily-plan'], queryFn: async () => (await api.get<Plan>('/planning/today')).data, refetchInterval: 60000 })
  const google = useQuery({ queryKey: ['google-status'], queryFn: async () => (await api.get('/googlecalendar/status')).data, staleTime: 60000 })
  const [pending, setPending] = useState(''), [scheduling, setScheduling] = useState<DailyAction | null>(null)
  const [start, setStart] = useState(''), [end, setEnd] = useState(''), [destination, setDestination] = useState('local')
  const fail = (error: any) => notify(error.response?.data?.detail || error.message || 'Could not update the action.', 'error')
  const complete = async (action: DailyAction) => {
    setPending(action.id)
    try { if (action.kind === 'habit') await api.post(`/habits/${action.sourceId}/complete`); else await api.post('/planning/complete', { actionId: action.id }); await queryClient.invalidateQueries({ queryKey: ['daily-plan'] }); window.dispatchEvent(new Event('notifications-updated')); onCompleted?.() } catch (error) { fail(error) } finally { setPending('') }
  }
  const openSchedule = (action: DailyAction) => { const now = new Date(); now.setMinutes(now.getMinutes() + 5); setStart(dateTimeInput(now.toISOString(), plan.data!.timeZone)); setEnd(dateTimeInput(new Date(now.getTime() + 1800000).toISOString(), plan.data!.timeZone)); setDestination('local'); setScheduling(action) }
  const schedule = async () => {
    if (!scheduling || !plan.data) return
    setPending(scheduling.id)
    try { const { data } = await api.post('/planning/schedule', { actionId: scheduling.id, startTime: zonedInputToIso(start, plan.data.timeZone), endTime: zonedInputToIso(end, plan.data.timeZone), destination }); notify(data.alreadyScheduled ? 'This action already has a calendar event.' : 'Time reserved on your calendar.', 'success'); setScheduling(null); navigate(data.actionUrl) } catch (error) { fail(error) } finally { setPending('') }
  }
  return <section className="glass-card p-5 space-y-4" aria-label="Daily plan">
    <header className="flex flex-wrap justify-between gap-2"><h2 className="text-lg font-semibold">Your daily plan</h2><span className="text-xs text-muted-foreground">{plan.data?.date} · {plan.data?.timeZone}</span></header>
    {plan.isPending && <p role="status">Loading your plan…</p>}
    {plan.isError && <div role="alert">Could not load your plan. <Button variant="outline" onClick={() => plan.refetch()}>Try again</Button></div>}
    {plan.data?.googleWarning && <p role="alert" className="text-sm text-amber-400">{plan.data.googleWarning} <Link to="/calendar">Open Calendar</Link></p>}
    {!!plan.data?.commitments.length && <div><h3 className="text-sm font-medium mb-2">On your calendar</h3><ul className="space-y-2">{plan.data.commitments.map(c => <li key={c.source + c.id}><Link className="block rounded-md border p-3 hover:bg-white/5" to={c.actionUrl}><span className="font-medium">{c.title}</span><span className="block text-xs text-muted-foreground">{c.isAllDay ? 'All day' : dateTimeInput(c.startTime, plan.data!.timeZone).slice(11)} · {c.source}</span></Link></li>)}</ul></div>}
    {plan.data && !plan.data.actions.length && <p className="text-muted-foreground">No actions due today. <Link className="underline" to="/habits">Add a habit</Link> or generate a mission with your AI Coach.</p>}
    <ul className="space-y-2">{plan.data?.actions.map(action => <li key={action.id} className={`border rounded-md p-3 flex flex-wrap items-center gap-3 ${action.completed ? 'opacity-60' : ''}`}>
      <div className="flex-1 min-w-[160px]"><Link className={`font-medium hover:underline ${action.completed ? 'line-through' : ''}`} to={action.actionUrl}>{action.title}</Link><p className="text-xs text-muted-foreground mt-1">{action.kind === 'followup' && action.priority === 0 ? 'Overdue · ' : ''}{action.reason}</p></div>
      {action.completed ? <span className="text-sm text-green-400">Completed</span> : <div className="flex gap-2"><Button size="sm" variant="outline" disabled={!!pending} onClick={() => openSchedule(action)}>Schedule</Button>{action.canComplete && <Button size="sm" disabled={!!pending} onClick={() => complete(action)}>{pending === action.id ? 'Saving…' : 'Complete'}</Button>}</div>}
    </li>)}</ul>
    <Modal open={!!scheduling} onClose={() => { if (!pending) setScheduling(null) }} title={'Schedule: ' + (scheduling?.title || '')}><form className="space-y-3" onSubmit={e => { e.preventDefault(); schedule() }}><p className="text-sm text-muted-foreground">Times in {plan.data?.timeZone}</p><label className="block">Start<input className={field} type="datetime-local" required value={start} onChange={e => setStart(e.target.value)} /></label><label className="block">End<input className={field} type="datetime-local" required value={end} onChange={e => setEnd(e.target.value)} /></label><label className="block">Calendar<select className={field} value={destination} onChange={e => setDestination(e.target.value)}><option value="local">LifeOS local</option>{google.data?.connected && <option value="google">LifeOS in Google Calendar</option>}</select></label><Button disabled={!!pending} type="submit">{pending ? 'Scheduling…' : 'Reserve time'}</Button></form></Modal>
  </section>
}
