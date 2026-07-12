import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Loader2, Trash2, MessageSquare } from 'lucide-react'

interface Application {
  id: string
  jobId: string
  jobTitle: string
  company: string
  status: string
  appliedDate?: string
  followUpDate?: string
  notes?: string
  statusHistory: { status: string; changedAt: string; notes?: string }[]
}

const statusColumns = [
  { key: 'applied', label: 'Applied', color: 'bg-blue-500/10 text-blue-500' },
  { key: 'phone_screen', label: 'Phone Screen', color: 'bg-yellow-500/10 text-yellow-500' },
  { key: 'interview', label: 'Interview', color: 'bg-purple-500/10 text-purple-500' },
  { key: 'offer', label: 'Offer', color: 'bg-green-500/10 text-green-500' },
  { key: 'rejected', label: 'Rejected', color: 'bg-destructive/10 text-destructive' },
]

export default function ApplicationsPage() {
  const [applications, setApplications] = useState<Application[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [draggedApp, setDraggedApp] = useState<Application | null>(null)

  useEffect(() => {
    loadApplications()
  }, [])

  const loadApplications = () => {
    setIsLoading(true)
    api.get('/applications')
      .then(({ data }) => setApplications(data))
      .catch(console.error)
      .finally(() => setIsLoading(false))
  }

  const moveStatus = async (appId: string, newStatus: string) => {
    try {
      await api.put(`/applications/${appId}/status`, { status: newStatus })
      loadApplications()
    } catch (err) {
      console.error(err)
    }
  }

  const deleteApplication = async (id: string) => {
    if (!confirm('Remove this application tracking?')) return
    try {
      await api.delete(`/applications/${id}`)
      loadApplications()
    } catch (err) {
      console.error(err)
    }
  }

  const addNote = async (id: string) => {
    const note = prompt('Enter note:')
    if (!note) return
    try {
      await api.post(`/applications/${id}/notes`, JSON.stringify(note), {
        headers: { 'Content-Type': 'application/json' }
      })
      loadApplications()
    } catch (err) {
      console.error(err)
    }
  }

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-7xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">Application Tracker</h1>
        <p className="text-muted-foreground mt-1">Track your job application pipeline</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-4">
        {statusColumns.map((column) => {
          const columnApps = applications.filter(a => a.status === column.key)
          return (
            <div
              key={column.key}
              className="bg-card border rounded-lg p-4 min-h-[200px]"
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault()
                if (draggedApp && draggedApp.status !== column.key) {
                  moveStatus(draggedApp.id, column.key)
                }
                setDraggedApp(null)
              }}
            >
              <div className={`text-sm font-medium px-3 py-2 rounded-md mb-4 ${column.color}`}>
                {column.label} ({columnApps.length})
              </div>
              <div className="space-y-3">
                {columnApps.map((app) => (
                  <div
                    key={app.id}
                    draggable
                    onDragStart={() => setDraggedApp(app)}
                    className="bg-background border rounded-md p-3 cursor-move hover:shadow-md transition-shadow"
                  >
                    <h4 className="font-medium text-sm">{app.jobTitle}</h4>
                    <p className="text-xs text-muted-foreground">{app.company}</p>
                    {app.appliedDate && (
                      <p className="text-xs text-muted-foreground mt-1">
                        Applied: {new Date(app.appliedDate).toLocaleDateString()}
                      </p>
                    )}
                    <div className="flex gap-1 mt-2">
                      <button onClick={() => addNote(app.id)} className="p-1 hover:bg-accent rounded">
                        <MessageSquare className="h-3 w-3 text-muted-foreground" />
                      </button>
                      <button onClick={() => deleteApplication(app.id)} className="p-1 hover:bg-destructive/10 rounded">
                        <Trash2 className="h-3 w-3 text-destructive" />
                      </button>
                    </div>
                    {app.notes && (
                      <p className="text-xs text-muted-foreground mt-2 line-clamp-2">{app.notes}</p>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
