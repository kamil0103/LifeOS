import { useState, useEffect, useCallback } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Upload, Sparkles, FileText, X, Save } from 'lucide-react'

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
  const [isDragging, setIsDragging] = useState(false)
  const [uploadProgress, setUploadProgress] = useState<string | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)

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
    } catch (err: any) {
      console.error(err)
      alert(err.response?.data?.detail || 'Failed to save institution')
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
    setSaveError(null)
    try {
      const { data } = await api.post('/transcripts/extract', { text: transcriptText })
      setExtractedData(data)
      setShowTranscriptModal(true)
    } catch (err: any) {
      console.error(err)
      setSaveError(err.response?.data?.detail || 'Extraction failed')
    } finally {
      setIsExtracting(false)
    }
  }

  const handleFileDrop = useCallback(async (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragging(false)
    setSaveError(null)
    
    const files = e.dataTransfer.files
    if (files.length === 0) return
    
    await uploadFile(files[0])
  }, [])

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setSaveError(null)
    await uploadFile(file)
  }

  const uploadFile = async (file: File) => {
    const allowed = ['.txt', '.pdf', '.doc', '.docx', '.rtf', '.csv', '.tsv', '.json', '.xml', '.html', '.htm']
    const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase()
    if (!allowed.includes(ext)) {
      setSaveError(`Unsupported file type. Allowed: ${allowed.join(', ')}`)
      return
    }

    setIsExtracting(true)
    setUploadProgress(`Uploading ${file.name}...`)

    try {
      const formData = new FormData()
      formData.append('file', file)

      const { data } = await api.post('/transcripts/extract-file', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      
      setExtractedData(data)
      setShowTranscriptModal(true)
    } catch (err: any) {
      console.error(err)
      setSaveError(err.response?.data?.detail || 'File upload/extraction failed')
    } finally {
      setIsExtracting(false)
      setUploadProgress(null)
    }
  }

  const saveExtracted = async () => {
    if (!extractedData) return
    setSaveError(null)
    try {
      await api.post('/transcripts/save', extractedData)
      alert('Education data saved!')
      setShowTranscriptModal(false)
      setTranscriptText('')
      setExtractedData(null)
      loadInstitutions()
    } catch (err: any) {
      console.error(err)
      setSaveError(err.response?.data?.detail || 'Save failed. Please check the data and try again.')
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="h-10 w-10 rounded-full border-2 border-blue-500/30 border-t-blue-500 animate-spin" />
      </div>
    )
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-header">Education</h1>
          <p className="text-white/40 mt-1 text-sm">Manage your institutions, degrees, and courses</p>
        </div>
        <Button onClick={() => setShowAddInst(true)} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">
          <Plus className="mr-2 h-4 w-4" />
          Add Institution
        </Button>
      </div>

      {/* File Upload / Paste Area */}
      <div className="glass-card p-6">
        <div className="flex items-center gap-3 mb-4">
          <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-purple-500/20 to-blue-500/20 border border-purple-500/20 flex items-center justify-center">
            <Upload className="h-5 w-5 text-purple-400" />
          </div>
          <div>
            <h3 className="text-lg font-semibold text-white">Transcript Upload</h3>
            <p className="text-sm text-white/40">Drag & drop a file or paste text to auto-extract with AI</p>
          </div>
        </div>

        {/* Drag & Drop Zone */}
        <div
          onDragOver={(e) => { e.preventDefault(); setIsDragging(true) }}
          onDragLeave={() => setIsDragging(false)}
          onDrop={handleFileDrop}
          className={`border-2 border-dashed rounded-xl p-8 text-center transition-all ${
            isDragging 
              ? 'border-blue-500/50 bg-blue-500/5' 
              : 'border-white/10 hover:border-white/20'
          }`}
        >
          <FileText className="h-10 w-10 text-white/20 mx-auto mb-3" />
          <p className="text-white/40 text-sm mb-2">
            Drag & drop a transcript file here, or <label className="text-blue-400 hover:text-blue-300 cursor-pointer underline"><input type="file" className="hidden" onChange={handleFileSelect} accept=".txt,.pdf,.doc,.docx,.rtf,.csv,.json,.xml,.html" />browse</label>
          </p>
          <p className="text-white/20 text-xs">Supported: PDF, TXT, DOC, DOCX, RTF, CSV, JSON, XML, HTML</p>
        </div>

        {/* Or paste text */}
        <div className="mt-4">
          <p className="text-white/30 text-xs uppercase tracking-wider mb-2">Or paste transcript text</p>
          <textarea
            className="w-full px-4 py-3 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            rows={4}
            placeholder="Paste transcript text here..."
            value={transcriptText}
            onChange={e => setTranscriptText(e.target.value)}
          />
          <div className="flex items-center gap-3 mt-3">
            <Button 
              onClick={extractTranscript} 
              disabled={isExtracting}
              className="bg-gradient-to-r from-purple-600 to-blue-500 hover:from-purple-500 hover:to-blue-400 border-0"
            >
              {isExtracting ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Sparkles className="mr-2 h-4 w-4" />
              )}
              {isExtracting ? (uploadProgress || 'Extracting...') : 'Extract with AI'}
            </Button>
            {transcriptText && (
              <Button variant="ghost" size="sm" onClick={() => setTranscriptText('')} className="text-white/40 hover:text-white hover:bg-white/5">
                <X className="h-4 w-4 mr-1" /> Clear
              </Button>
            )}
          </div>
        </div>

        {saveError && (
          <div className="mt-4 text-sm text-red-400 bg-red-500/10 border border-red-500/20 p-3 rounded-lg">
            {saveError}
          </div>
        )}
      </div>

      {/* Add Institution Form */}
      {showAddInst && (
        <div className="glass-card p-6">
          <h3 className="text-lg font-semibold text-white mb-4">Add Institution</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <input
              type="text"
              placeholder="Institution Name"
              value={newInst.name}
              onChange={(e) => setNewInst({ ...newInst, name: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            />
            <select
              value={newInst.institutionType}
              onChange={(e) => setNewInst({ ...newInst, institutionType: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
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
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            />
          </div>
          <div className="mt-4 flex gap-2">
            <Button onClick={addInstitution} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">Save</Button>
            <Button variant="outline" onClick={() => setShowAddInst(false)} className="border-white/10 hover:bg-white/5">Cancel</Button>
          </div>
        </div>
      )}

      {/* Institutions List */}
      <div className="space-y-4">
        {institutions.length === 0 && (
          <div className="text-center py-12 glass-card">
            <p className="text-white/30">No institutions yet.</p>
            <p className="text-white/20 text-sm mt-1">Upload a transcript or add an institution manually.</p>
          </div>
        )}

        {institutions.map((inst) => (
          <div key={inst.id} className="glass-card p-6">
            <div className="flex items-start justify-between mb-4">
              <div>
                <h3 className="text-lg font-semibold text-white">{inst.name}</h3>
                <p className="text-sm text-white/40">
                  {inst.institutionType} {inst.location && `· ${inst.location}`}
                </p>
              </div>
              <Button variant="ghost" size="sm" onClick={() => deleteInstitution(inst.id)} className="text-red-400 hover:text-red-300 hover:bg-red-500/5">
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>

            {inst.degrees.length > 0 && (
              <div className="mt-4 space-y-3">
                <h4 className="text-xs font-medium text-white/30 uppercase tracking-wider">Degrees</h4>
                {inst.degrees.map((deg) => (
                  <div key={deg.id} className="pl-4 border-l-2 border-blue-500/20 py-2">
                    <div className="flex items-center justify-between">
                      <p className="font-medium text-white">{deg.degreeName}</p>
                      <span className="text-xs bg-white/5 px-2 py-1 rounded text-white/60">{deg.degreeType}</span>
                    </div>
                    {deg.field && <p className="text-sm text-white/40">{deg.field}</p>}
                    <p className="text-sm text-white/30">
                      {deg.startDate} — {deg.isCurrent ? 'Present' : deg.endDate}
                      {deg.gpa && ` · GPA: ${deg.gpa}`}
                    </p>
                    {deg.courses.length > 0 && (
                      <div className="mt-2 flex flex-wrap gap-2">
                        {deg.courses.map((c) => (
                          <span key={c.id} className="text-xs bg-white/5 px-2 py-1 rounded text-white/50">
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
                <h4 className="text-xs font-medium text-white/30 uppercase tracking-wider mb-2">
                  Unassigned Courses
                </h4>
                <div className="flex flex-wrap gap-2">
                  {inst.unassignedCourses.map((c) => (
                    <span key={c.id} className="text-xs bg-white/5 px-2 py-1 rounded text-white/50">
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
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50" onClick={() => setShowTranscriptModal(false)}>
          <div className="glass-card p-6 w-[600px] max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h2 className="text-xl font-semibold text-white mb-4">Review Extracted Data</h2>
            
            {saveError && (
              <div className="mb-4 text-sm text-red-400 bg-red-500/10 border border-red-500/20 p-3 rounded-lg">
                {saveError}
              </div>
            )}
            
            <div className="space-y-4">
              <div>
                <h3 className="text-sm font-medium text-white/40">Institution</h3>
                <p className="font-medium text-white">{extractedData.institution?.name || 'Not detected'}</p>
                <p className="text-sm text-white/40">{extractedData.institution?.type} {extractedData.institution?.location && `· ${extractedData.institution.location}`}</p>
              </div>
              <div>
                <h3 className="text-sm font-medium text-white/40">Degree</h3>
                <p className="font-medium text-white">{extractedData.degree?.name || 'Not detected'}</p>
                <p className="text-sm text-white/40">{extractedData.degree?.field} {extractedData.degree?.type && `· ${extractedData.degree.type}`}</p>
                {extractedData.degree?.gpa && <p className="text-sm text-white/40">GPA: {extractedData.degree.gpa}</p>}
              </div>
              <div>
                <h3 className="text-sm font-medium text-white/40">Courses ({extractedData.courses?.length || 0})</h3>
                <div className="space-y-1 max-h-48 overflow-y-auto">
                  {extractedData.courses?.map((c: any, i: number) => (
                    <div key={i} className="text-sm text-white/60 border-b border-white/5 pb-1">
                      {c.code} {c.name} {c.grade && `· Grade: ${c.grade}`} {c.credits && `· Credits: ${c.credits}`}
                    </div>
                  ))}
                </div>
              </div>
            </div>
            <div className="flex gap-2 mt-6">
              <Button onClick={saveExtracted} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">
                <Save className="mr-2 h-4 w-4" /> Save to Education
              </Button>
              <Button variant="outline" onClick={() => setShowTranscriptModal(false)} className="border-white/10 hover:bg-white/5">Cancel</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
