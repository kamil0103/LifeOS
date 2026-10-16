import { useEffect, useState } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Modal } from '@/components/ui/modal'
interface Source { type: string; id: string; label: string; url: string }
interface Evidence { id: string; label: string; notes?: string; available: boolean; url?: string }
export default function SkillEvidencePanel({ skill, onClose }: { skill: { id: string; name: string }; onClose: () => void }) {
  const [sources, setSources] = useState<Source[]>([]), [items, setItems] = useState<Evidence[]>([]), [selected, setSelected] = useState(''), [notes, setNotes] = useState(''), [filter, setFilter] = useState('')
  const [loading, setLoading] = useState(true), [busy, setBusy] = useState(false), [error, setError] = useState(''), [attempt, setAttempt] = useState(0)
  useEffect(() => { let active = true; setLoading(true); setError(''); Promise.all([api.get('/skills/evidence-options'), api.get(`/skills/${skill.id}/evidence`)]).then(([options, links]) => { if (!active) return; setSources(options.data); setItems(links.data); const query = new URLSearchParams(window.location.search), initial = options.data.find((s: Source) => s.type === query.get('sourceType') && s.id === query.get('sourceId')); if (initial) setSelected(initial.type + ':' + initial.id) }).catch(() => { if (active) setError('Could not load evidence. Try again.') }).finally(() => { if (active) setLoading(false) }); return () => { active = false } }, [skill.id, attempt])
  const add = async () => { const source = sources.find(s => s.type + ':' + s.id === selected); if (!source || busy) return; setBusy(true); setError(''); try { await api.post(`/skills/${skill.id}/evidence`, { sourceType: source.type, sourceId: source.id, notes }); setItems((await api.get(`/skills/${skill.id}/evidence`)).data); setNotes('') } catch (e: any) { setError(e.response?.data?.detail || 'Could not link evidence. Your notes are kept.') } finally { setBusy(false) } }
  const remove = async (id: string) => { setBusy(true); setError(''); try { await api.delete(`/skills/${skill.id}/evidence/${id}`); setItems(current => current.filter(item => item.id !== id)) } catch { setError('Could not unlink evidence. Try again.') } finally { setBusy(false) } }
  return <Modal open onClose={() => { if (!busy) onClose() }} title={`Evidence: ${skill.name}`}>
    <p className="text-sm text-muted-foreground">Link your own work, projects, courses and certificates. Skill status is self-reported; linking evidence does not automatically change it.</p>
    {loading && <p role="status">Loading evidence…</p>}{error && <p role="alert" className="text-red-400">{error} {sources.length === 0 && <Button variant="outline" onClick={() => setAttempt(n => n + 1)}>Retry evidence</Button>}</p>}
    {!loading && <><ul className="my-4 space-y-2">{items.map(item => <li key={item.id} className="border rounded p-3"><p>{item.available && item.url ? <a className="underline" href={item.url}>{item.label}</a> : item.label}</p>{item.notes && <p className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</p>}<Button disabled={busy} variant="outline" size="sm" onClick={() => remove(item.id)}>Unlink {item.label}</Button></li>)}</ul>{!items.length && <p className="text-muted-foreground">No evidence linked yet.</p>}
      <label className="block mt-3">Find a source<input className="w-full border rounded p-2 bg-background" value={filter} onChange={e => setFilter(e.target.value)} /></label>
      <label className="block mt-3">Evidence source<select className="w-full border rounded p-2 bg-background" disabled={busy} value={selected} onChange={e => setSelected(e.target.value)}><option value="">Choose a record</option>{sources.filter(s => (s.label + ' ' + s.type).toLowerCase().includes(filter.toLowerCase()) || s.type + ':' + s.id === selected).map(s => <option key={s.type + s.id} value={s.type + ':' + s.id}>{s.type}: {s.label}</option>)}</select></label>
      <label className="block mt-3">Evidence notes<textarea className="w-full border rounded p-2 bg-background" maxLength={1500} value={notes} onChange={e => setNotes(e.target.value)} placeholder="What did you build, apply or learn?" /></label><Button className="mt-3" disabled={busy || !selected} onClick={add}>{busy ? 'Saving…' : 'Link evidence'}</Button>
    </>}
  </Modal>
}
