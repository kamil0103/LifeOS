import DocumentPreview from './DocumentPreview'
import DocumentLinksEditor from './DocumentLinksEditor'
import { Link, useSearchParams } from 'react-router-dom'
import { notify } from '@/lib/notify'
import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Download, Trash2, FileText } from 'lucide-react'

interface DocumentItem {
  id: string
  type: string
  filename: string
  generatedAt: string
  jobId?: string
  jobTitle?: string
  updatedAt: string
  applicationId?: string
  applicationCompany?: string
  applicationStatus?: string
  sourceResumeVersionId?: string
  sourceResumeVersionTitle?: string
}

export default function DocumentsPage() {
  const [params] = useSearchParams()
  const [search, setSearch] = useState(''), [type, setType] = useState(''), [jobFilter, setJobFilter] = useState(params.get('job') || ''), [error, setError] = useState('')
  const [applicationFilter, setApplicationFilter] = useState(params.get('application') || '')
  const [editing, setEditing] = useState<DocumentItem | null>(null)
  const [documents, setDocuments] = useState<DocumentItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  useEffect(() => {
    loadDocuments()
  }, [])

  const loadDocuments = async () => {
    setIsLoading(true)
    try {
      const { data } = await api.get('/documents')
      setDocuments(data); setError('')
    } catch (err) {
      console.error(err); setError('Could not load documents.')
    } finally {
      setIsLoading(false)
    }
  }

  const downloadDocument = async (id: string, filename: string) => {
    try {
      const response = await api.get(`/documents/${id}/download`, { responseType: 'blob' })
      const blob = new Blob([response.data], { type: 'application/pdf' })
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = filename
      a.click()
      window.URL.revokeObjectURL(url)
    } catch (err) {
      console.error(err)
      notify('Download failed')
    }
  }

  const deleteDocument = async (id: string) => {
    if (!confirm('Delete this document?')) return
    setDeletingId(id)
    try {
      await api.delete(`/documents/${id}`)
      setDocuments(documents.filter(d => d.id !== id))
    } catch (err) {
      console.error(err); notify((err as any).response?.data?.detail || 'Could not delete the document.', 'error')
    } finally {
      setDeletingId(null)
    }
  }

  const visible = documents.filter(d => (!type || d.type === type) && (!jobFilter || d.jobId === jobFilter) && (!applicationFilter || d.applicationId === applicationFilter) && [d.filename, d.jobTitle, d.applicationCompany, d.sourceResumeVersionTitle].filter(Boolean).join(' ').toLowerCase().includes(search.trim().toLowerCase()))
  const jobs = Array.from(new Map(documents.filter(d => d.jobId).map(d => [d.jobId!, d.jobTitle || 'Linked job'])).entries())
  const applications = Array.from(new Map(documents.filter(d => d.applicationId).map(d => [d.applicationId!, [d.jobTitle, d.applicationCompany].filter(Boolean).join(' · ') || 'Linked application'])).entries())
  const getTypeLabel = (type: string) => {
    switch (type) {
      case 'resume': return 'Resume'
      case 'cover_letter': return 'Cover Letter'
      default: return type
    }
  }

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-4xl mx-auto">
      <h1 className="text-2xl font-bold mb-3">Documents</h1>
      <p className="text-sm text-muted-foreground mb-4">Find generated resumes, cover letters and job preparation files.</p>
      <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-5"><input className="border rounded-md p-2 bg-background" aria-label="Search documents" placeholder="Filename, role or source version" value={search} onChange={e => setSearch(e.target.value)} /><select className="border rounded-md p-2 bg-background" aria-label="Filter document type" value={type} onChange={e => setType(e.target.value)}><option value="">All document types</option><option value="resume">Resumes</option><option value="cover_letter">Cover letters</option><option value="qa_sheet">Interview preparation</option></select><select className="border rounded-md p-2 bg-background" aria-label="Filter documents by job" value={jobFilter} onChange={e => setJobFilter(e.target.value)}><option value="">All jobs</option>{jobFilter && !jobs.some(([id]) => id === jobFilter) && <option value={jobFilter}>Selected job</option>}{jobs.map(([id, title]) => <option key={id} value={id}>{title}</option>)}</select><select className="border rounded-md p-2 bg-background" aria-label="Filter documents by application" value={applicationFilter} onChange={e => setApplicationFilter(e.target.value)}><option value="">All applications</option>{applicationFilter && !applications.some(([id]) => id === applicationFilter) && <option value={applicationFilter}>Selected application</option>}{applications.map(([id, title]) => <option key={id} value={id}>{title}</option>)}</select></div>
      {applicationFilter && <Link className="text-sm underline block mb-4" to={'/jobs/applications?application=' + applicationFilter}>Open application</Link>}
      {error && <p role="alert">{error} <Button onClick={loadDocuments}>Try again</Button></p>}
      {editing && <DocumentLinksEditor key={editing.id} document={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); loadDocuments() }} />}
      {jobFilter && <Link className="text-sm underline block mb-4" to={'/jobs?job=' + jobFilter}>Open job details</Link>}
      {!error && documents.length > 0 && visible.length === 0 && <p className="text-muted-foreground py-6">No documents match these filters.</p>}

      {documents.length === 0 && !error ? (
        <div className="bg-card border rounded-lg p-8 text-center">
          <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
          <p className="text-muted-foreground">No documents generated yet.</p>
          <p className="text-sm text-muted-foreground mt-1">
            Go to the Resume Editor to generate your first resume or cover letter.
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {visible.map(doc => (
            <div key={doc.id} className="bg-card border rounded-lg p-4 flex flex-wrap gap-3 items-center justify-between">
              <div className="flex items-center gap-4">
                <div className="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center">
                  <FileText className="h-5 w-5 text-primary" />
                </div>
                <div>
                  <p className="font-medium">{getTypeLabel(doc.type)}</p>
                  <p className="text-sm text-muted-foreground">{doc.filename}</p>
                  {doc.jobTitle && (
                    <p className="text-xs text-muted-foreground">For: <Link className="underline" to={'/jobs?job=' + doc.jobId}>{doc.jobTitle}</Link></p>
                  )}
                  {doc.applicationId && <p className="text-xs mt-1">Application: <Link className="underline" to={'/jobs/applications?application=' + doc.applicationId}>{doc.applicationCompany || 'Open application'}{doc.applicationStatus ? ' · ' + doc.applicationStatus.replace(/_/g, ' ') : ''}</Link></p>}
                  {doc.sourceResumeVersionId && <p className="text-xs mt-1">Source version: <Link className="underline" to={'/resume?version=' + doc.sourceResumeVersionId}>{doc.sourceResumeVersionTitle || 'View version'}</Link></p>}
                  <p className="text-xs text-muted-foreground">
                    {new Date(doc.generatedAt).toLocaleString()}
                  </p>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                <DocumentPreview id={doc.id} filename={doc.filename} />
                <Button size="sm" variant="outline" onClick={() => setEditing(doc)}>Edit links</Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => downloadDocument(doc.id, doc.filename)}
                >
                  <Download className="mr-2 h-4 w-4" />
                  Download
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="text-destructive"
                  onClick={() => deleteDocument(doc.id)}
                  disabled={deletingId === doc.id}
                >
                  {deletingId === doc.id ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Trash2 className="h-4 w-4" />
                  )}
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
