import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Upload, Sparkles } from 'lucide-react'

interface Course {
  id: string
  code?: string
  name: string
  grade?: string
  credits?: number
  term?: string
  isMajorRelated: boolean
}

interface Degree {
  id: string
  institutionId?: string
  institutionName?: string
  degreeName: string
  degreeType: string
  field?: string
  startDate?: string
  endDate?: string
  gpa?: string
  honors?: string
  isCurrent: boolean
  courses: Course[]
}

interface Institution {
  id: string
  name: string
  institutionType: string
  location?: string
  degrees: Degree[]
  unassignedCourses: Course[]
}

export default function EducationPage() {
  const [institutions, setInstitutions] = useState<Institution[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [showAddInst, setShowAddInst] = useState(false)
  const [newInst, setNewInst] = useState({ name: '', institutionType: 'university', location: '' })
  const [transcriptText, setTranscriptText] = useState('')
  const [isExtracting, setIsExtracting] = useState(false)
  const [extractedData, setExtractedData] = useState<any | null>(null)
  const [showTranscriptModal, setShowTranscriptModal] = useState(false)

  useEffect(() => {
    loadInstitutions()
  }, [])

  const loadInstitutions = () => {
    setIsLoading(true)
    api.get('/education/institutions')
      .then(({ data }) => setInstitutions(data))
      .catch(console.error)
      .finally(() => setIsLoading(false))
  }

  const addInstitution = async () => {
    if (!newInst.name) return
    try {
      await api.post('/education/institutions', newInst)
      setNewInst({ name: '', institutionType: 'university', location: '' })
      setShowAddInst(false)
      loadInstitutions()
    } catch (err) {
      console.error(err)
    }
  }

  const deleteInstitution = async (id: string) => {
    if (!confirm('Delete this institution and all associated degrees/courses?')) return
    try {
      await api.delete(`/education/institutions/${id}`)
      loadInstitutions()
    } catch (err) {
      console.error(err)
    }
  }

  const extractTranscript = async () => {
    if (!transcriptText.trim() || transcriptText.length < 50) {
      alert('Please paste at least 50 characters of transcript text')
      return
    }
    setIsExtracting(true)
    try {
      const { data } = await api.post('/transcripts/extract', { text: transcriptText })
      setExtractedData(data)
      setShowTranscriptModal(true)
    } catch (err) {
      console.error(err)
      alert('Extraction failed')
    } finally {
      setIsExtracting(false)
    }
  }

  const saveExtracted = async () => {
    if (!extractedData) return
    try {
      await api.post('/transcripts/save', extractedData)
      alert('Education data saved!')
      setShowTranscriptModal(false)
      setTranscriptText('')
      setExtractedData(null)
      loadInstitutions()
    } catch (err) {
      console.error(err)
      alert('Save failed')
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
          <h1 className="text-3xl font-bold tracking-tight">Education</h1>
          <p className="text-muted-foreground mt-1">Manage your institutions, degrees, and courses</p>
        </div>
        <Button onClick={() => setShowAddInst(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Add Institution
        </Button>
      </div>

      {/* Transcript Upload */}
      <div className="mb-6 bg-card border rounded-lg p-6 shadow-sm">
        <h3 className="text-lg font-semibold mb-2 flex items-center gap-2">
          <Upload className="h-5 w-5 text-primary" />
          Transcript Upload
        </h3>
        <p className="text-sm text-muted-foreground mb-3">Paste your transcript text and let AI extract courses, institutions, and degrees.</p>
        <textarea
          className="w-full px-3 py-2 border rounded-md bg-background text-sm mb-3"
          rows={4}
          placeholder="Paste transcript text here..."
          value={transcriptText}
          onChange={e => setTranscriptText(e.target.value)}
        />
        <Button onClick={extractTranscript} disabled={isExtracting}>
          {isExtracting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Sparkles className="mr-2 h-4 w-4" />}
          Extract with AI
        </Button>
      </div>

      {showAddInst && (
        <div className="mb-6 bg-card border rounded-lg p-6 shadow-sm">
          <h3 className="text-lg font-semibold mb-4">Add Institution</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <input
              type="text"
              placeholder="Institution Name"
              value={newInst.name}
              onChange={(e) => setNewInst({ ...newInst, name: e.target.value })}
              className="px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
            <select
              value={newInst.institutionType}
              onChange={(e) => setNewInst({ ...newInst, institutionType: e.target.value })}
              className="px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <option value="university">University</option>
              <option value="community_college">Community College</option>
              <option value="high_school">High School</option>
              <option value="certificate_organization">Certificate Organization</option>
              <option value="other">Other</option>
            </select>
            <input
              type="text"
              placeholder="Location"
              value={newInst.location}
              onChange={(e) => setNewInst({ ...newInst, location: e.target.value })}
              className="px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
          </div>
          <div className="mt-4 flex gap-2">
            <Button onClick={addInstitution}>Save</Button>
            <Button variant="outline" onClick={() => setShowAddInst(false)}>Cancel</Button>
          </div>
        </div>
      )}

      <div className="space-y-6">
        {institutions.length === 0 && (
          <div className="text-center py-12 text-muted-foreground">
            No institutions yet. Add one above to get started.
          </div>
        )}

        {institutions.map((inst) => (
          <div key={inst.id} className="bg-card border rounded-lg p-6 shadow-sm">
            <div className="flex items-start justify-between mb-4">
              <div>
                <h3 className="text-lg font-semibold">{inst.name}</h3>
                <p className="text-sm text-muted-foreground">
                  {inst.institutionType} {inst.location && `· ${inst.location}`}
                </p>
              </div>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" onClick={() => deleteInstitution(inst.id)}>
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              </div>
            </div>

            {inst.degrees.length > 0 && (
              <div className="mt-4 space-y-3">
                <h4 className="text-sm font-medium text-muted-foreground uppercase tracking-wider">Degrees</h4>
                {inst.degrees.map((deg) => (
                  <div key={deg.id} className="pl-4 border-l-2 border-primary/20 py-2">
                    <div className="flex items-center justify-between">
                      <p className="font-medium">{deg.degreeName}</p>
                      <span className="text-xs bg-secondary px-2 py-1 rounded">{deg.degreeType}</span>
                    </div>
                    {deg.field && <p className="text-sm text-muted-foreground">{deg.field}</p>}
                    <p className="text-sm text-muted-foreground">
                      {deg.startDate} — {deg.isCurrent ? 'Present' : deg.endDate}
                      {deg.gpa && ` · GPA: ${deg.gpa}`}
                    </p>
                    {deg.courses.length > 0 && (
                      <div className="mt-2 flex flex-wrap gap-2">
                        {deg.courses.map((c) => (
                          <span key={c.id} className="text-xs bg-accent px-2 py-1 rounded">
                            {c.code} {c.name}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}

            {inst.unassignedCourses.length > 0 && (
              <div className="mt-4">
                <h4 className="text-sm font-medium text-muted-foreground uppercase tracking-wider mb-2">
                  Unassigned Courses
                </h4>
                <div className="flex flex-wrap gap-2">
                  {inst.unassignedCourses.map((c) => (
                    <span key={c.id} className="text-xs bg-accent px-2 py-1 rounded">
                      {c.code} {c.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Transcript Review Modal */}
      {showTranscriptModal && extractedData && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setShowTranscriptModal(false)}>
          <div className="bg-card border rounded-lg p-6 w-[600px] max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h2 className="text-xl font-semibold mb-4">Review Extracted Data</h2>
            <div className="space-y-4">
              <div>
                <h3 className="text-sm font-medium text-muted-foreground">Institution</h3>
                <p className="font-medium">{extractedData.institution?.name}</p>
                <p className="text-sm">{extractedData.institution?.type} · {extractedData.institution?.location}</p>
              </div>
              <div>
                <h3 className="text-sm font-medium text-muted-foreground">Degree</h3>
                <p className="font-medium">{extractedData.degree?.name}</p>
                <p className="text-sm">{extractedData.degree?.field} · {extractedData.degree?.type}</p>
                {extractedData.degree?.gpa && <p className="text-sm">GPA: {extractedData.degree.gpa}</p>}
              </div>
              <div>
                <h3 className="text-sm font-medium text-muted-foreground">Courses ({extractedData.courses?.length})</h3>
                <div className="space-y-1">
                  {extractedData.courses?.map((c: any, i: number) => (
                    <div key={i} className="text-sm border-b pb-1">
                      {c.code} {c.name} · Grade: {c.grade} · Credits: {c.credits}
                    </div>
                  ))}
                </div>
              </div>
            </div>
            <div className="flex gap-2 mt-4">
              <Button onClick={saveExtracted}>Save to Education</Button>
              <Button variant="outline" onClick={() => setShowTranscriptModal(false)}>Cancel</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
