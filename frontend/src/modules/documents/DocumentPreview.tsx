import { useEffect, useRef, useState } from 'react'
import api from '@/lib/api'
import { Modal } from '@/components/ui/modal'
import { Button } from '@/components/ui/button'
export default function DocumentPreview({ id, filename }: { id: string; filename: string }) {
  const [open, setOpen] = useState(false), [url, setUrl] = useState(''), [busy, setBusy] = useState(false), [error, setError] = useState('')
  const alive = useRef(true)
  useEffect(() => { alive.current = true; return () => { alive.current = false } }, [])
  useEffect(() => () => { if (url) URL.revokeObjectURL(url) }, [url])
  const preview = async () => { setOpen(true); setBusy(true); setError(''); setUrl(''); try { const { data } = await api.get(`/documents/${id}/preview`, { responseType: 'blob' }); const next = URL.createObjectURL(new Blob([data], { type: 'application/pdf' })); if (alive.current) setUrl(next); else URL.revokeObjectURL(next) } catch { if (alive.current) setError('Could not preview this stored PDF. Retry or use Download.') } finally { if (alive.current) setBusy(false) } }
  return <><Button size="sm" variant="outline" disabled={busy} onClick={preview}>Preview</Button><Modal open={open} onClose={() => { if (!busy) { setOpen(false); setUrl('') } }} title={filename}>{busy && <p role="status">Loading PDF…</p>}{error && <p role="alert">{error} <Button variant="outline" onClick={preview}>Retry preview</Button></p>}{url && <div className="space-y-3"><iframe title={filename + ' PDF preview'} src={url} className="w-full h-[65vh] border rounded" /><a className="underline text-sm" href={url} download={filename}>Download if your browser cannot display the PDF</a></div>}</Modal></>
}
