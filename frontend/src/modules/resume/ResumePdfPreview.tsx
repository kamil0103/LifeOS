import { useEffect, useRef, useState } from 'react'
import api from '@/lib/api'
import { Modal } from '@/components/ui/modal'
import { Button } from '@/components/ui/button'
export default function ResumePdfPreview({ data }: { data: unknown }) {
  const [open, setOpen] = useState(false), [url, setUrl] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState('')
  const alive = useRef(true)
  useEffect(() => { alive.current = true; return () => { alive.current = false } }, [])
  useEffect(() => () => { if (url) URL.revokeObjectURL(url) }, [url])
  const preview = async () => { setOpen(true); setBusy(true); setError(''); setUrl(''); try { const result = await api.post('/resume-draft/preview', { data }, { responseType: 'blob' }); const next = URL.createObjectURL(new Blob([result.data], { type: 'application/pdf' })); if (alive.current) setUrl(next); else URL.revokeObjectURL(next) } catch { setError('Could not render this PDF preview. Your draft is unchanged. Try again.') } finally { setBusy(false) } }
  return <><Button variant="outline" disabled={busy} onClick={preview}>Preview PDF</Button><Modal open={open} onClose={() => { if (!busy) { setOpen(false); setUrl('') } }} title="Resume PDF preview">{busy && <p role="status">Rendering current draft…</p>}{error && <p role="alert">{error} <Button variant="outline" onClick={preview}>Retry preview</Button></p>}{url && <div className="space-y-3"><p className="text-sm">Snapshot of the current draft. Previewing does not create a document or saved version.</p><iframe title="Resume PDF preview" src={url} className="w-full h-[65vh] border rounded" /><a className="underline text-sm" href={url} download="resume-preview.pdf">Download preview if your browser cannot display it</a></div>}</Modal></>
}
