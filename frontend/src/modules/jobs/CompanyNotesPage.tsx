import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Star } from 'lucide-react'

interface CompanyNote {
  id: string
  companyName: string
  notes?: string
  rating?: number
  createdAt: string
}

export default function CompanyNotesPage() {
  const [notes, setNotes] = useState<CompanyNote[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [showAdd, setShowAdd] = useState(false)
  const [newNote, setNewNote] = useState({ companyName: '', notes: '', rating: 3 })

  useEffect(() => {
    loadNotes()
  }, [])

  const loadNotes = () => {
    setIsLoading(true)
    api.get('/companynotes')
      .then(({ data }) => setNotes(data))
      .catch(console.error)
      .finally(() => setIsLoading(false))
  }

  const addNote = async () => {
    if (!newNote.companyName) return
    try {
      await api.post('/companynotes', newNote)
      setNewNote({ companyName: '', notes: '', rating: 3 })
      setShowAdd(false)
      loadNotes()
    } catch (err) {
      console.error(err)
    }
  }

  const deleteNote = async (id: string) => {
    if (!confirm('Delete this note?')) return
    try {
      await api.delete(`/companynotes/${id}`)
      loadNotes()
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
    <div className="p-8 max-w-5xl mx-auto">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Company Notes</h1>
          <p className="text-muted-foreground mt-1">Research and track companies</p>
        </div>
        <Button onClick={() => setShowAdd(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Add Note
        </Button>
      </div>

      {showAdd && (
        <div className="bg-card border rounded-lg p-6 shadow-sm mb-6">
          <h3 className="text-lg font-semibold mb-4">Add Company Note</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <input
              placeholder="Company Name"
              value={newNote.companyName}
              onChange={(e) => setNewNote({ ...newNote, companyName: e.target.value })}
              className="px-3 py-2 rounded-md border bg-background text-sm"
            />
            <div className="flex items-center gap-2">
              <span className="text-sm">Rating:</span>
              {[1, 2, 3, 4, 5].map((r) => (
                <button
                  key={r}
                  onClick={() => setNewNote({ ...newNote, rating: r })}
                  className="p-1"
                >
                  <Star
                    className={`h-5 w-5 ${r <= newNote.rating ? 'text-yellow-500 fill-yellow-500' : 'text-muted-foreground'}`}
                  />
                </button>
              ))}
            </div>
            <textarea
              placeholder="Notes about the company..."
              value={newNote.notes}
              onChange={(e) => setNewNote({ ...newNote, notes: e.target.value })}
              className="px-3 py-2 rounded-md border bg-background text-sm min-h-[80px] md:col-span-2"
            />
          </div>
          <div className="mt-4 flex gap-2">
            <Button onClick={addNote}>Save</Button>
            <Button variant="outline" onClick={() => setShowAdd(false)}>Cancel</Button>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {notes.length === 0 && (
          <div className="text-center py-12 text-muted-foreground md:col-span-2">
            No company notes yet. Add research on companies you're interested in.
          </div>
        )}

        {notes.map((note) => (
          <div key={note.id} className="bg-card border rounded-lg p-6 shadow-sm">
            <div className="flex items-start justify-between">
              <div>
                <h3 className="font-semibold">{note.companyName}</h3>
                {note.rating && (
                  <div className="flex gap-1 mt-1">
                    {[1, 2, 3, 4, 5].map((r) => (
                      <Star
                        key={r}
                        className={`h-4 w-4 ${r <= note.rating! ? 'text-yellow-500 fill-yellow-500' : 'text-muted-foreground'}`}
                      />
                    ))}
                  </div>
                )}
              </div>
              <Button variant="ghost" size="sm" onClick={() => deleteNote(note.id)}>
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
            {note.notes && <p className="mt-3 text-sm text-muted-foreground">{note.notes}</p>}
          </div>
        ))}
      </div>
    </div>
  )
}
