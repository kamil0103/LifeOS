import { useEffect, useState } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'

interface Row { code?: string; name: string; term?: string; grade?: string; credits?: string | number }
interface Preview { index: number; status: 'new' | 'existing' | 'batch_duplicate'; existingCourse?: Row; duplicateOf?: number }
export default function TranscriptReview({ data, saving, error, onSave, onCancel }: {
  data: { institution: { name: string }; degree?: { name?: string }; courses?: Row[] }
  saving: boolean; error: string | null; onSave: (data: any) => void; onCancel: () => void
}) {
  const [preview, setPreview] = useState<Preview[] | null>(null)
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [previewError, setPreviewError] = useState('')
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    let active = true
    setPreview(null); setPreviewError('')
    api.post('/transcripts/preview', data).then(({ data: rows }: { data: Preview[] }) => {
      if (!active) return
      setPreview(rows); setSelected(new Set(rows.filter(row => row.status === 'new').map(row => row.index)))
    }).catch(err => { if (active) setPreviewError(err.response?.data?.detail || 'Could not check for duplicate courses. Try again.') })
    return () => { active = false }
  }, [data, attempt])
  return <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
    <section role="dialog" aria-modal="true" aria-labelledby="transcript-review-title" className="glass-card p-6 w-full max-w-2xl max-h-[85vh] overflow-y-auto">
      <h2 id="transcript-review-title" className="text-xl font-semibold text-white">Review Extracted Data</h2>
      <p className="text-white/70 mt-2">{data.institution?.name || 'Missing institution'} {data.degree?.name && `· ${data.degree.name}`}</p>
      <p className="text-sm text-white/60 mt-2">Select courses to import. Existing courses are preserved. Different terms are separate records. The server checks duplicates again when saving.</p>
      {!preview && !previewError && <p role="status" className="mt-4 text-white/60">Checking existing courses…</p>}
      {previewError && <div role="alert" className="mt-4 text-red-400">{previewError} <Button variant="outline" onClick={() => setAttempt(n => n + 1)}>Retry check</Button></div>}
      {error && <p role="alert" className="mt-4 text-red-400">{error}</p>}
      <div className="space-y-3 mt-4">{preview?.map(row => {
        const course = data.courses?.[row.index]
        if (!course) return null
        return <div key={row.index} className="rounded-lg border border-white/10 p-3 text-sm">
          <label className="flex items-start gap-3 text-white">
            <input type="checkbox" disabled={saving || row.status === 'existing'} checked={selected.has(row.index)} onChange={e => setSelected(current => { const next = new Set(current); e.target.checked ? next.add(row.index) : next.delete(row.index); return next })} aria-label={`Import ${course.code || ''} ${course.name}`} />
            <span>{course.code} {course.name}<span className="block text-white/60">Imported: {course.term || 'No term'} · Grade: {course.grade || '—'} · Credits: {course.credits ?? '—'}</span></span>
          </label>
          {row.status === 'existing' && <p className="mt-2 text-amber-300">Already saved: {row.existingCourse?.name} · Grade: {row.existingCourse?.grade || '—'} · Credits: {row.existingCourse?.credits ?? '—'}. Skipped; edit the saved course in Education to change it.</p>}
          {row.status === 'batch_duplicate' && <p className="mt-2 text-amber-300">Matches import row {(row.duplicateOf ?? 0) + 1}. Select only the row you want to keep; if both are selected, the first wins.</p>}
        </div>
      })}</div>
      {preview && <p role="status" className="text-white/60 text-sm mt-4">{selected.size} of {preview.length} courses selected</p>}
      <div className="flex gap-2 mt-5">
        <Button disabled={saving || !preview || !data.institution?.name} onClick={() => onSave({ ...data, courses: (data.courses || []).filter((_, index) => selected.has(index)) })}>{saving ? 'Saving…' : 'Save to Education'}</Button>
        <Button disabled={saving} variant="outline" onClick={onCancel}>Cancel</Button>
      </div>
    </section>
  </div>
}
