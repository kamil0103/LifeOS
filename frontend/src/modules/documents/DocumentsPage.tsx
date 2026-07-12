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
}

export default function DocumentsPage() {
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
      setDocuments(data)
    } catch (err) {
      console.error(err)
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
      alert('Download failed')
    }
  }

  const deleteDocument = async (id: string) => {
    if (!confirm('Delete this document?')) return
    setDeletingId(id)
    try {
      await api.delete(`/documents/${id}`)
      setDocuments(documents.filter(d => d.id !== id))
    } catch (err) {
      console.error(err)
    } finally {
      setDeletingId(null)
    }
  }

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
      <h1 className="text-2xl font-bold mb-6">Documents</h1>

      {documents.length === 0 ? (
        <div className="bg-card border rounded-lg p-8 text-center">
          <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
          <p className="text-muted-foreground">No documents generated yet.</p>
          <p className="text-sm text-muted-foreground mt-1">
            Go to the Resume Editor to generate your first resume or cover letter.
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {documents.map(doc => (
            <div key={doc.id} className="bg-card border rounded-lg p-4 flex items-center justify-between">
              <div className="flex items-center gap-4">
                <div className="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center">
                  <FileText className="h-5 w-5 text-primary" />
                </div>
                <div>
                  <p className="font-medium">{getTypeLabel(doc.type)}</p>
                  <p className="text-sm text-muted-foreground">{doc.filename}</p>
                  {doc.jobTitle && (
                    <p className="text-xs text-muted-foreground">For: {doc.jobTitle}</p>
                  )}
                  <p className="text-xs text-muted-foreground">
                    {new Date(doc.generatedAt).toLocaleString()}
                  </p>
                </div>
              </div>
              <div className="flex gap-2">
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
