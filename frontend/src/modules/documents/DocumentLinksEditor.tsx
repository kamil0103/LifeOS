import { useEffect, useState } from 'react'
import api from '@/lib/api'
import { Modal } from '@/components/ui/modal'
import { Button } from '@/components/ui/button'
import { useUnsavedChanges } from '@/hooks/useUnsavedChanges'
export interface LinkedDocument { id: string; filename: string; updatedAt: string; jobId?: string; applicationId?: string; sourceResumeVersionId?: string }
const field = 'w-full border rounded p-2 bg-background text-sm'
export default function DocumentLinksEditor({ document, onClose, onSaved }: { document: LinkedDocument; onClose: () => void; onSaved: () => void }) {
  const initial = { jobId: document.jobId || '', applicationId: document.applicationId || '', sourceResumeVersionId: document.sourceResumeVersionId || '' }
  const [draft, setDraft] = useState(initial), [jobs, setJobs] = useState<{ id: string; title: string; company: string }[]>([]), [apps, setApps] = useState<{ id: string; jobId: string; jobTitle: string; company: string }[]>([]), [versions, setVersions] = useState<{ id: string; title: string }[]>([]), [ready, setReady] = useState(false), [loading, setLoading] = useState(true), [busy, setBusy] = useState(false), [error, setError] = useState('')
  const dirty = JSON.stringify(initial) !== JSON.stringify(draft)
  useUnsavedChanges(dirty)
  const load = async () => { setLoading(true); setReady(false); setError(''); try { const [j, a, v] = await Promise.all([api.get('/jobs'), api.get('/job-applications'), api.get('/resume-versions')]); setJobs(j.data); setApps(a.data); setVersions(v.data); setReady(true) } catch { setError('Could not load link choices. Try again.') } finally { setLoading(false) } }
  useEffect(() => { load() }, [])
  const close = () => { if (!busy && (!dirty || window.confirm('Discard unsaved document links?'))) onClose() }
  const save = async () => { if (busy) return; setBusy(true); setError(''); try { await api.put(`/documents/${document.id}/links`, { jobId: draft.jobId || null, applicationId: draft.applicationId || null, sourceResumeVersionId: draft.sourceResumeVersionId || null, expectedUpdatedAt: document.updatedAt }); onSaved() } catch (e: any) { setError(e.response?.data?.detail || 'Could not save document links. Your choices are retained.') } finally { setBusy(false) } }
  const choicesReady = ready && !loading
  return <Modal open onClose={close} title={'Links: ' + document.filename}><form className="space-y-4" onSubmit={e => { e.preventDefault(); save() }}><p className="text-sm text-muted-foreground">Reference links do not change the resume or cover letter selected in Application Tracker. A source version records provenance; linking it does not verify or change the PDF contents.</p>{loading && <p role="status">Loading link choices…</p>}{error && <p role="alert" className="text-destructive">{error}</p>}
    <label className="block text-sm">Application<select className={field} value={draft.applicationId} disabled={loading || !ready || busy} onChange={e => { const app = apps.find(a => a.id === e.target.value); setDraft({ ...draft, applicationId: e.target.value, jobId: app?.jobId || draft.jobId }) }}><option value="">No application</option>{draft.applicationId && !apps.some(a => a.id === draft.applicationId) && <option value={draft.applicationId}>Previously linked application</option>}{apps.map(a => <option key={a.id} value={a.id}>{a.jobTitle} · {a.company}</option>)}</select></label>
    <label className="block text-sm">Saved job<select className={field} value={draft.jobId} disabled={loading || !ready || busy || !!draft.applicationId} onChange={e => setDraft({ ...draft, jobId: e.target.value })}><option value="">No job</option>{draft.jobId && !jobs.some(j => j.id === draft.jobId) && <option value={draft.jobId}>Previously linked job</option>}{jobs.map(j => <option key={j.id} value={j.id}>{j.title} · {j.company}</option>)}</select></label>
    <label className="block text-sm">Source resume version<select className={field} value={draft.sourceResumeVersionId} disabled={loading || !ready || busy} onChange={e => setDraft({ ...draft, sourceResumeVersionId: e.target.value })}><option value="">No source version</option>{draft.sourceResumeVersionId && !versions.some(v => v.id === draft.sourceResumeVersionId) && <option value={draft.sourceResumeVersionId}>Previously linked version</option>}{versions.map(v => <option key={v.id} value={v.id}>{v.title}</option>)}</select></label>
    <div className="flex gap-2"><Button disabled={loading || busy || !choicesReady}>{busy ? 'Saving…' : 'Save links'}</Button><Button type="button" variant="outline" disabled={busy} onClick={close}>Cancel</Button>{error && <Button type="button" variant="outline" disabled={busy} onClick={load}>Reload choices</Button>}</div>
  </form></Modal>
}
