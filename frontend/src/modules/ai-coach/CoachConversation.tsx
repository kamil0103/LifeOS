import { useEffect, useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import api from '@/lib/api'
import { notify } from '@/lib/notify'
import { useAuthStore } from '@/hooks/useAuthStore'
import { useUnsavedChanges } from '@/hooks/useUnsavedChanges'
import { Button } from '@/components/ui/button'
interface Turn { id: string; role: string; content: string; createdAt: string }
interface Memory { id: string; content: string; updatedAt: string }
interface Preferences { includeProfile: boolean; includeProgress: boolean; includeCalendar: boolean; includeJournal: boolean }
interface Context { date: string; timeZone: string; builtAt: string; sections: { source: string; content: string }[]; warning?: string }
const field = 'w-full border rounded-md p-2 bg-background text-sm'
export default function CoachConversation() {
  const userId = useAuthStore(state => state.user?.id) || 'current'
  const client = useQueryClient(), draftKey = 'lifeos-coach-draft:' + userId
  const history = useQuery({ queryKey: ['coach-history', userId], queryFn: async () => (await api.get<Turn[]>('/aicoach/history')).data })
  const memories = useQuery({ queryKey: ['coach-memories', userId], queryFn: async () => (await api.get<Memory[]>('/aicoach/memories')).data })
  const context = useQuery({ queryKey: ['coach-context', userId], queryFn: async () => (await api.get<Context>('/aicoach/context')).data })
  const preferences = useQuery({ queryKey: ['coach-preferences', userId], queryFn: async () => (await api.get<Preferences>('/aicoach/preferences')).data })
  const [draft, setDraft] = useState(() => localStorage.getItem(draftKey) || ''), [sending, setSending] = useState(false), [mutating, setMutating] = useState(false), [sendError, setSendError] = useState('')
  const [memoryText, setMemoryText] = useState(''), [memoryId, setMemoryId] = useState<string | null>(null), [memorySnapshot, setMemorySnapshot] = useState('')
  const requestId = useRef<string | null>(localStorage.getItem(draftKey + ':request-id')), bottom = useRef<HTMLDivElement>(null)
  const busy = sending || mutating
  useUnsavedChanges(!!draft.trim() || memoryText !== memorySnapshot)
  useEffect(() => { try { if (draft) localStorage.setItem(draftKey, draft); else { localStorage.removeItem(draftKey); localStorage.removeItem(draftKey + ':request-id') } } catch { /* The in-memory draft is still available. */ } }, [draft, draftKey])
  useEffect(() => { bottom.current?.scrollIntoView?.({ block: 'nearest' }) }, [history.data?.length, sending])
  const fail = (e: any) => notify(e.response?.data?.detail || 'Could not update AI Coach. Try again.', 'error')
  const refreshContext = () => client.invalidateQueries({ queryKey: ['coach-context', userId] })
  const send = async () => {
    if (busy || !draft.trim() || history.isPending || history.isError) return
    setSending(true); setSendError(''); requestId.current ||= crypto.randomUUID(); try { localStorage.setItem(draftKey + ':request-id', requestId.current) } catch { /* In-memory retries retain the identifier. */ }
    try { await api.post('/aicoach/chat', { message: draft.trim(), requestId: requestId.current }); setDraft(''); requestId.current = null; await history.refetch(); await refreshContext() } catch (e: any) { setSendError(e.response?.data?.detail || 'Could not send your message. Your draft is saved; try again.') } finally { setSending(false) }
  }
  const updatePreference = async (key: keyof Preferences, value: boolean) => {
    if (!preferences.data || busy) return
    setMutating(true)
    try { await api.put('/aicoach/preferences', { ...preferences.data, [key]: value }); await preferences.refetch(); await refreshContext() } catch (e) { fail(e) } finally { setMutating(false) }
  }
  const saveMemory = async () => {
    if (busy || !memoryText.trim()) return
    setMutating(true)
    try { if (memoryId) await api.put('/aicoach/memories/' + memoryId, { content: memoryText.trim() }); else await api.post('/aicoach/memories', { content: memoryText.trim() }); setMemoryText(''); setMemorySnapshot(''); setMemoryId(null); await memories.refetch(); await refreshContext(); notify('Saved note updated.', 'success') } catch (e) { fail(e) } finally { setMutating(false) }
  }
  const deleteMemory = async (item: Memory) => {
    if (!window.confirm('Remove this saved note? Clear the conversation too to remove earlier mentions.')) return
    setMutating(true)
    try { await api.delete('/aicoach/memories/' + item.id); if (memoryId === item.id) { setMemoryId(null); setMemoryText(''); setMemorySnapshot('') } await memories.refetch(); await refreshContext() } catch (e) { fail(e) } finally { setMutating(false) }
  }
  const clear = async () => {
    if (busy || !window.confirm('Clear this conversation and unsent draft? Saved notes will remain.')) return
    setMutating(true)
    try { await api.delete('/aicoach/history'); setDraft(''); requestId.current = null; setSendError(''); await history.refetch() } catch (e) { fail(e) } finally { setMutating(false) }
  }
  return <div className="space-y-5">
    <section className="glass-card p-5 space-y-4" aria-label="Coach conversation"><header className="flex flex-wrap justify-between gap-3"><h2 className="font-semibold text-lg">Chat with AI Coach</h2><Button size="sm" variant="outline" disabled={busy || !history.data?.length} onClick={clear}>Clear conversation</Button></header><p className="text-xs text-muted-foreground">Shows the latest 100 saved messages. Replies use your last 12 messages and enabled app context. Save important goals as notes below to carry them across cleared conversations.</p>
      {history.isPending && <p role="status">Loading conversation…</p>}{history.isError && <p role="alert">Could not load conversation. <button className="underline" onClick={() => history.refetch()}>Try again</button></p>}
      <div className="space-y-3 max-h-[550px] overflow-y-auto" aria-live="polite">{history.data?.length === 0 && <p className="text-sm text-muted-foreground">Ask about your next step, recent progress or a challenge you are working through.</p>}{history.data?.map(turn => <article key={turn.id} className={`rounded-lg p-4 ${turn.role === 'user' ? 'bg-blue-500/10' : 'bg-white/5'}`}><p className="text-xs font-medium text-muted-foreground mb-2">{turn.role === 'user' ? 'You' : 'AI Coach'} · {new Date(turn.createdAt).toLocaleString()}</p><p className="text-sm whitespace-pre-wrap break-words">{turn.content}</p></article>)}{sending && <p role="status" className="text-sm text-muted-foreground">Thinking with your recent conversation and app context…</p>}<div ref={bottom} /></div>
      {sendError && <p role="alert" className="text-sm text-amber-400">{sendError}</p>}
      <form className="space-y-2" onSubmit={e => { e.preventDefault(); send() }}><label className="block text-sm">Your message<textarea className={field + ' min-h-[100px] mt-1'} maxLength={4000} value={draft} disabled={sending} onChange={e => { setDraft(e.target.value); requestId.current = null; localStorage.removeItem(draftKey + ':request-id') }} placeholder="Ask for advice, planning help or a follow-up…" onKeyDown={e => { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); send() } }} /></label><div className="flex flex-wrap justify-between gap-2"><span className="text-xs text-muted-foreground">{draft.length}/4000 · Ctrl/⌘ + Enter to send</span><Button disabled={busy || !draft.trim() || history.isPending || history.isError}>{sending ? 'Sending…' : 'Send message'}</Button></div></form>
    </section>
    <section className="glass-card p-5 space-y-3"><h2 className="font-semibold">Saved notes</h2><p className="text-xs text-muted-foreground">Only notes you save here become persistent memory. They are included in Coach requests to your configured AI provider. {memories.data?.length || 0}/20 notes.</p>{memories.isError && <p role="alert">Could not load saved notes. <button className="underline" onClick={() => memories.refetch()}>Try again</button></p>}<ul className="space-y-2">{memories.data?.map(item => <li key={item.id} className="border rounded-md p-3"><p className="text-sm whitespace-pre-wrap">{item.content}</p><div className="flex gap-3 mt-2"><button className="text-xs underline" disabled={busy} onClick={() => { if (memoryText !== memorySnapshot && !window.confirm('Discard the unsaved note?')) return; setMemoryId(item.id); setMemoryText(item.content); setMemorySnapshot(item.content) }}>Edit note</button><button className="text-xs underline" disabled={busy} onClick={() => deleteMemory(item)}>Remove note</button></div></li>)}</ul><form className="space-y-2" onSubmit={e => { e.preventDefault(); saveMemory() }}><label className="block text-sm">{memoryId ? 'Edit saved note' : 'Remember a goal or constraint'}<textarea className={field + ' mt-1'} maxLength={500} value={memoryText} disabled={busy} onChange={e => setMemoryText(e.target.value)} placeholder="For example: I want to focus on embedded C++ roles and can study for an hour after work." /></label><div className="flex gap-2"><Button size="sm" disabled={busy || !memoryText.trim() || memories.isError || memories.isPending}>{memoryId ? 'Save changes' : 'Save note'}</Button>{(memoryId || memoryText) && <Button size="sm" variant="outline" disabled={busy} onClick={() => { setMemoryId(null); setMemoryText(''); setMemorySnapshot('') }}>Cancel note</Button>}</div></form></section>
    <details className="glass-card p-5"><summary className="font-semibold cursor-pointer">Context and privacy</summary><div className="space-y-4 mt-4"><p className="text-sm text-muted-foreground">Choose what the Coach uses for chat and future missions. Journal text is off by default. Google event titles follow your separate Calendar sharing choice; otherwise the Coach sees busy times.</p>{preferences.isError && <p role="alert">Could not load preferences. <button className="underline" onClick={() => preferences.refetch()}>Try again</button></p>}{preferences.data && <div className="grid sm:grid-cols-2 gap-3">{([['includeProfile', 'Profile and skills'], ['includeProgress', 'Habits, applications and coding progress'], ['includeCalendar', 'Calendar commitments'], ['includeJournal', 'Recent journal text (opt in)']] as const).map(([key, label]) => <label className="text-sm flex items-center gap-2" key={key}><input type="checkbox" disabled={busy} checked={preferences.data![key]} onChange={e => updatePreference(key, e.target.checked)} />{label}</label>)}</div>}<div className="flex flex-wrap justify-between gap-2"><span className="text-xs text-muted-foreground">{context.data?.date} · {context.data?.timeZone} · Context preview built {context.data && new Date(context.data.builtAt).toLocaleTimeString()}</span><button className="text-xs underline" onClick={() => context.refetch()}>Refresh preview</button></div>{context.isError && <p role="alert">Could not build context preview. Try again.</p>}{context.data?.warning && <p className="text-sm text-amber-400">{context.data.warning}</p>}{context.data?.sections.map(section => <details key={section.source} className="border rounded-md p-3"><summary className="text-sm cursor-pointer">{section.source}</summary><pre className="mt-2 text-xs whitespace-pre-wrap break-words max-h-64 overflow-auto">{section.content}</pre></details>)}<p className="text-xs text-muted-foreground">The preview reflects current app data; the next request rebuilds it. Turning a source off affects future requests. Clear conversation separately to remove previously shared chat history.</p></div></details>
  </div>
}
