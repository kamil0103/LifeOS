import { useEffect, useMemo, useState } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'

interface Row { code?: string; name: string; term?: string; grade?: string; credits?: string | number }
interface Preview { index: number; status: 'new' | 'existing' | 'batch_duplicate'; existingCourse?: Row & { id: string }; duplicateOf?: number; fingerprint?: string }
interface Destination { id: string; name: string; degrees: { id: string; degreeName: string; field?: string }[] }
export default function TranscriptReview({ data, institutions = [], saving, error, onSave, onCancel }: {
  data: { institution: { name: string }; degree?: { name?: string }; courses?: Row[] }
  institutions?: Destination[]
  saving: boolean; error: string | null; onSave: (data: any) => void; onCancel: () => void
}) {
  const [institutionId, setInstitutionId] = useState('')
  const [degreeChoice, setDegreeChoice] = useState('auto')
  const [preview, setPreview] = useState<Preview[] | null>(null)
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [previewError, setPreviewError] = useState('')
  const [attempt, setAttempt] = useState(0)
  const request = useMemo(() => ({ ...data, institutionId: institutionId || undefined, degreeId: ['auto', 'unassigned'].includes(degreeChoice) ? undefined : degreeChoice, unassigned: degreeChoice === 'unassigned' }), [data, institutionId, degreeChoice])
  const destination = institutions.find(institution => institution.id === institutionId)
  useEffect(() => {
    let active = true
    setPreview(null); setPreviewError(''); setSelected(new Set())
    api.post('/transcripts/preview', request).then(({ data: rows }: { data: Preview[] }) => {
      if (!active) return
      setPreview(rows); setSelected(new Set(rows.filter(row => row.status === 'new').map(row => row.index)))
    }).catch(err => { if (active) setPreviewError(err.response?.data?.detail || 'Could not check for duplicate courses. Try again.') })
    return () => { active = false }
  }, [request, attempt])
  return <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
    <section role="dialog" aria-modal="true" aria-labelledby="transcript-review-title" className="glass-card p-6 w-full max-w-2xl max-h-[85vh] overflow-y-auto">
      <h2 id="transcript-review-title" className="text-xl font-semibold text-white">Review Extracted Data</h2>
      <p className="text-white/70 mt-2">Extracted: {data.institution?.name || 'Missing institution'} {data.degree?.name && `· ${data.degree.name}`}</p>
      <div className="grid gap-3 mt-4 sm:grid-cols-2">
        <label className="text-sm text-white/70">Destination institution
          <select aria-label="Destination institution" disabled={saving} value={institutionId} onChange={e => { setPreview(null); setSelected(new Set()); setInstitutionId(e.target.value); setDegreeChoice('auto') }} className="block w-full mt-1 rounded border border-white/10 bg-slate-900 p-2 text-white">
            <option value="">Match or create extracted institution</option>
            {institutions.map(institution => <option key={institution.id} value={institution.id}>{institution.name}</option>)}
          </select>
        </label>
        <label className="text-sm text-white/70">Destination degree
          <select aria-label="Destination degree" disabled={saving} value={degreeChoice} onChange={e => { setPreview(null); setSelected(new Set()); setDegreeChoice(e.target.value) }} className="block w-full mt-1 rounded border border-white/10 bg-slate-900 p-2 text-white">
            <option value="auto">{data.degree?.name ? 'Match or create extracted degree' : 'No extracted degree'}</option>
            <option value="unassigned">Unassigned courses (no degree)</option>
            {destination?.degrees.map(degree => <option key={degree.id} value={degree.id}>{degree.degreeName}{degree.field ? ` · ${degree.field}` : ''}</option>)}
          </select>
        </label>
      </div>
      <p className="text-sm text-white/60 mt-2">Select an existing institution to choose one of its degrees. Changing the destination resets approvals and checks for duplicates again.</p>
      <p className="text-sm text-white/60 mt-2">New courses are selected automatically. Existing courses stay unchanged unless you approve their imported grade and credits. Blank values preserve saved values; names, terms and other details stay unchanged.</p>
      {!preview && !previewError && <p role="status" className="mt-4 text-white/60">Checking existing courses…</p>}
      {previewError && <div role="alert" className="mt-4 text-red-400">{previewError} <Button variant="outline" onClick={() => setAttempt(n => n + 1)}>Retry check</Button></div>}
      {error && <p role="alert" className="mt-4 text-red-400">{error}</p>}
      {preview && <Button disabled={saving} variant="outline" className="mt-3" onClick={() => setAttempt(n => n + 1)}>Refresh comparison</Button>}
      <div className="space-y-3 mt-4">{preview?.map(row => {
        const course = data.courses?.[row.index]
        if (!course) return null
        const existing = row.status === 'existing'
        return <div key={row.index} className="rounded-lg border border-white/10 p-3 text-sm">
          <label className="flex items-start gap-3 text-white">
            <input type="checkbox" disabled={saving || (existing && (!row.fingerprint || !row.existingCourse?.id))} checked={selected.has(row.index)} onChange={e => setSelected(current => { const next = new Set(current); e.target.checked ? next.add(row.index) : next.delete(row.index); return next })} aria-label={`${existing ? 'Approve grade and credits for' : 'Import'} ${course.code || ''} ${course.name}`} />
            <span>{course.code} {course.name}<span className="block text-white/60">Imported: {course.term || 'No term'} · Grade: {course.grade || '—'} · Credits: {course.credits ?? '—'}</span></span>
          </label>
          {existing && <p className="mt-2 text-amber-300">Already saved: {row.existingCourse?.name} · Grade: {row.existingCourse?.grade || '—'} · Credits: {row.existingCourse?.credits ?? '—'}. {selected.has(row.index) ? 'Approved: apply imported grade and credits.' : 'Keeping saved values. Select to approve an update.'}</p>}
          {row.status === 'batch_duplicate' && <p className="mt-2 text-amber-300">Matches import row {(row.duplicateOf ?? 0) + 1}. Select only the row you want to keep; if both are selected, the first wins.</p>}
        </div>
      })}</div>
      {preview && <p role="status" className="text-white/60 text-sm mt-4">{selected.size} of {preview.length} courses selected · {preview.filter(row => row.status === 'existing' && selected.has(row.index)).length} updates approved</p>}
      <div className="flex gap-2 mt-5">
        <Button disabled={saving || !preview || (!institutionId && !data.institution?.name)} onClick={() => onSave({ ...request, courses: preview!.filter(row => selected.has(row.index)).map(row => ({ ...data.courses![row.index], credits: data.courses![row.index].credits == null ? undefined : String(data.courses![row.index].credits), updateExisting: row.status === 'existing', existingCourseId: row.status === 'existing' ? row.existingCourse?.id : undefined, expectedFingerprint: row.status === 'existing' ? row.fingerprint : undefined })) })}>{saving ? 'Saving…' : 'Save to Education'}</Button>
        <Button disabled={saving} variant="outline" onClick={onCancel}>Cancel</Button>
      </div>
    </section>
  </div>
}
