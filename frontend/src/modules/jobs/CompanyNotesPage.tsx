import { useState } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/lib/api'
import { notify } from '@/lib/notify'
import { useUnsavedChanges } from '@/hooks/useUnsavedChanges'
import { Modal } from '@/components/ui/modal'
import { Button } from '@/components/ui/button'
import { safeProjectUrl } from '../portfolio/PortfolioPreview'
const researchFields = [
  ['website', 'Website'], ['industry', 'Industry'], ['products', 'Products and services'],
  ['technologyStack', 'Technology stack'], ['culture', 'Culture and working style'],
  ['interviewPreparation', 'Interview preparation'],
] as const
interface Note { id: string; companyName: string; notes?: string; rating?: number; createdAt: string; updatedAt?: string; website?: string; industry?: string; products?: string; technologyStack?: string; culture?: string; interviewPreparation?: string }
interface RelatedJob { id: string; title: string; location?: string; salaryRange?: string; status: string; applicationId?: string; applicationStatus?: string }
const field = 'w-full border rounded-md p-2 bg-background'
function RelatedJobs({ note }: { note: Note }) {
  const [expanded, setExpanded] = useState(false)
  const query = useQuery({ queryKey: ['company-related-jobs', note.id, note.companyName], enabled: expanded, queryFn: async () => (await api.get<RelatedJob[]>(`/companynotes/${note.id}/jobs`)).data })
  return <section className="mt-3 border-t pt-3"><Button size="sm" variant="outline" aria-expanded={expanded} onClick={() => setExpanded(!expanded)}>{expanded ? 'Hide related jobs' : 'View related jobs'}</Button>{expanded && <div className="mt-3 space-y-2">
    <p className="text-xs text-muted-foreground">Saved jobs matching this company name, ignoring case and outer spaces.</p>
    {query.isPending && <p role="status">Loading related jobs…</p>}
    {query.isError && <p role="alert">Could not load related jobs. <button className="underline" onClick={() => query.refetch()}>Try again</button></p>}
    {!query.isPending && !query.isError && !query.data?.length && <p className="text-sm">No saved jobs match this company yet.</p>}
    {query.data?.map(job => <article key={job.id} className="border rounded p-3 text-sm"><Link className="font-medium underline" to={'/jobs?job=' + job.id}>{job.title}</Link><p>{[job.location, job.salaryRange].filter(Boolean).join(' · ')}</p><p>Job: {job.status.replace(/_/g, ' ')}</p>{job.applicationId ? <Link className="underline" to={'/jobs/applications?application=' + job.applicationId}>Application: {job.applicationStatus?.replace(/_/g, ' ')}</Link> : <p className="text-muted-foreground">No application tracked</p>}</article>)}
  </div>}</section>
}
const emptyDraft = (company: string) => ({ companyName: company, notes: '', rating: '3', website: '', industry: '', products: '', technologyStack: '', culture: '', interviewPreparation: '' })
export default function CompanyNotesPage() {
  const [params] = useSearchParams(), company = params.get('company') || ''
  const query = useQuery({ queryKey: ['company-notes'], queryFn: async () => (await api.get<Note[]>('/companynotes')).data })
  const [search, setSearch] = useState(company), [open, setOpen] = useState(false), [id, setId] = useState<string | null>(null), [busy, setBusy] = useState(false)
  const [draft, setDraft] = useState(emptyDraft(company)), [snapshot, setSnapshot] = useState(''), [expectedUpdatedAt, setExpectedUpdatedAt] = useState<string | undefined>(), [saveError, setSaveError] = useState('')
  const dirty = open && JSON.stringify(draft) !== snapshot
  useUnsavedChanges(dirty)
  const edit = (note?: Note) => { const value = { companyName: note?.companyName || company, notes: note?.notes || '', rating: note?.rating?.toString() || '', website: note?.website || '', industry: note?.industry || '', products: note?.products || '', technologyStack: note?.technologyStack || '', culture: note?.culture || '', interviewPreparation: note?.interviewPreparation || '' }; setDraft(note ? value : emptyDraft(company)); setId(note?.id || null); setExpectedUpdatedAt(note?.updatedAt); setSnapshot(JSON.stringify(note ? value : emptyDraft(company))); setSaveError(''); setOpen(true) }
  const close = () => { if (!busy && (!dirty || window.confirm('Discard unsaved company research?'))) setOpen(false) }
  const save = async () => { if (busy) return; setBusy(true); setSaveError(''); try { const body = { ...draft, rating: draft.rating ? Number(draft.rating) : null, ...(id && expectedUpdatedAt ? { expectedUpdatedAt } : {}) }; if (id) await api.put('/companynotes/' + id, body); else await api.post('/companynotes', body); setOpen(false); await query.refetch(); notify('Company research saved.', 'success') } catch (e: any) { setSaveError(e.response?.data?.detail || 'Could not save company research. Your edits are retained.') } finally { setBusy(false) } }
  const remove = async (note: Note) => { if (!window.confirm('Delete this company note?')) return; setBusy(true); try { await api.delete('/companynotes/' + note.id); await query.refetch() } catch { notify('Could not delete the note.', 'error') } finally { setBusy(false) } }
  const notes = (query.data || []).filter(n => [n.companyName, n.notes, ...researchFields.map(([key]) => n[key])].filter(Boolean).join(' ').toLowerCase().includes(search.trim().toLowerCase()))
  return <div className="max-w-5xl mx-auto space-y-5">
    <header className="flex flex-wrap justify-between gap-3"><div><h1 className="page-header">Company Notes</h1><p className="text-muted-foreground">Research, interview preparation and impressions.</p></div><Button onClick={() => edit()}>Add note</Button></header>
    <input className={field} aria-label="Search company notes" placeholder="Company or research keywords" value={search} onChange={e => setSearch(e.target.value)} />
    {company && <Link className="text-sm underline" to="/jobs">Back to jobs</Link>}
    {query.isPending && <p role="status">Loading research…</p>}
    {query.isError && <p role="alert">Could not load company research. <Button onClick={() => query.refetch()}>Try again</Button></p>}
    {!query.isPending && !query.isError && !notes.length && <p className="glass-card p-6 text-muted-foreground">No research matches this search. Add a note for {company || 'a company'}.</p>}
    <ul className="space-y-3">{notes.map(note => <li className="glass-card p-4 break-words" key={note.id}>
      <h2 className="font-medium">{note.companyName}</h2><p className="text-xs text-amber-400 my-1">{note.rating ? '★'.repeat(Math.max(0, Math.min(note.rating, 5))) : 'Not rated'}</p>
      <dl className="grid sm:grid-cols-2 gap-3 mb-3">{researchFields.map(([key, label]) => note[key] && <div key={key}><dt className="text-sm font-medium">{label}</dt><dd className="text-sm whitespace-pre-wrap text-muted-foreground">{key === 'website' && safeProjectUrl(note.website) ? <a className="underline" href={safeProjectUrl(note.website)} target="_blank" rel="noopener noreferrer">{note.website}</a> : note[key]}</dd></div>)}</dl>
      <p className="whitespace-pre-wrap text-sm">{note.notes || 'No general notes yet.'}</p><p className="text-xs text-muted-foreground mt-2">Added {new Date(note.createdAt).toLocaleDateString()}</p>
      <div className="flex gap-2 mt-3"><Button size="sm" variant="outline" disabled={busy} onClick={() => edit(note)}>Edit research</Button><Button size="sm" variant="ghost" disabled={busy} onClick={() => remove(note)}>Delete</Button></div><RelatedJobs note={note} />
    </li>)}</ul>
    <Modal open={open} onClose={close} title={id ? 'Edit company research' : 'Add company research'}><form className="space-y-3" onSubmit={e => { e.preventDefault(); save() }}>
      {saveError && <p role="alert" className="text-destructive">{saveError}</p>}
      <label className="block">Company<input required maxLength={200} className={field} value={draft.companyName} onChange={e => setDraft({ ...draft, companyName: e.target.value })} /></label>
      <label className="block">Rating<select className={field} value={draft.rating} onChange={e => setDraft({ ...draft, rating: e.target.value })}><option value="">Not rated</option>{[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{n} / 5</option>)}</select></label>
      {researchFields.map(([key, label]) => <label className="block" key={key}>{label}{key === 'website' || key === 'industry' ? <input type={key === 'website' ? 'url' : 'text'} maxLength={key === 'website' ? 2048 : 200} className={field} value={draft[key]} onChange={e => setDraft({ ...draft, [key]: e.target.value })} /> : <textarea maxLength={10000} className={field + ' min-h-24'} value={draft[key]} onChange={e => setDraft({ ...draft, [key]: e.target.value })} />}</label>)}
      <label className="block">General notes<textarea maxLength={20000} className={field + ' min-h-[140px]'} value={draft.notes} onChange={e => setDraft({ ...draft, notes: e.target.value })} /></label><Button disabled={busy}>{busy ? 'Saving…' : 'Save research'}</Button>
    </form></Modal>
  </div>
}
