import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, FileText, Save, Download } from 'lucide-react'

interface ResumeProfile {
  fullName: string
  email: string
  phone: string
  location: string
  linkedIn: string
  portfolio: string
  github: string
  summary: string
}

interface ResumeExperience {
  id: string
  title: string
  company: string
  location: string
  startDate?: string
  endDate?: string
  isCurrent: boolean
  bullets: string
}

interface ResumeEducation {
  id: string
  school: string
  degree: string
  field: string
  startDate?: string
  endDate?: string
  isCurrent: boolean
  gpa?: string
  honors?: string
}

interface ResumeSkillGroup {
  category: string
  skills: string[]
}

interface ResumeProject {
  id: string
  name: string
  description: string
  technologies: string
  link: string
}

interface ResumeCertification {
  name: string
  organization: string
  date?: string
}

interface ResumeData {
  title: string
  template: string
  sectionOrder: string[]
  profile: ResumeProfile
  experience: ResumeExperience[]
  education: ResumeEducation[]
  skills: ResumeSkillGroup[]
  projects: ResumeProject[]
  certifications: ResumeCertification[]
}

interface ResumeVersion {
  id: string
  title: string
  template: string
  createdAt: string
}

const ALL_SECTIONS = ['summary', 'experience', 'education', 'skills', 'projects', 'certifications']

export default function ResumeEditorPage() {
  const [resumeData, setResumeData] = useState<ResumeData | null>(null)
  const [versions, setVersions] = useState<ResumeVersion[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isGenerating, setIsGenerating] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [activeTab, setActiveTab] = useState<'edit' | 'versions'>('edit')

  useEffect(() => {
    loadData()
  }, [])

  const loadData = async () => {
    setIsLoading(true)
    try {
      const { data } = await api.get('/resume-versions')
      setVersions(data)
      if (data.length > 0) {
        const latest = await api.get(`/resume-versions/${data[0].id}`)
        setResumeData(latest.data.data)
      } else {
        const fresh = await api.get('/documents/resume-data')
        setResumeData(fresh.data)
      }
    } catch (err: any) {
      if (err.response?.status === 404) {
        try {
          const fresh = await api.get('/documents/resume-data')
          setResumeData(fresh.data)
        } catch (e) {
          console.error(e)
        }
      }
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const generatePdf = async () => {
    if (!resumeData) return
    setIsGenerating(true)
    try {
      const response = await api.post('/documents/resume', {
        title: resumeData.title,
        template: resumeData.template,
        sectionOrder: resumeData.sectionOrder,
      }, { responseType: 'blob' })
      const blob = new Blob([response.data], { type: 'application/pdf' })
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `${resumeData.profile.fullName || 'resume'}_resume.pdf`
      a.click()
      window.URL.revokeObjectURL(url)
    } catch (err) {
      console.error(err)
      alert('Failed to generate resume')
    } finally {
      setIsGenerating(false)
    }
  }

  const saveVersion = async () => {
    if (!resumeData) return
    setIsSaving(true)
    try {
      await api.post('/resume-versions', {
        title: resumeData.title,
        template: resumeData.template,
        data: resumeData,
      })
      const { data } = await api.get('/resume-versions')
      setVersions(data)
      alert('Resume version saved!')
    } catch (err) {
      console.error(err)
      alert('Failed to save version')
    } finally {
      setIsSaving(false)
    }
  }

  const loadVersion = async (id: string) => {
    try {
      const { data } = await api.get(`/resume-versions/${id}`)
      if (data.data) {
        setResumeData(data.data)
        setActiveTab('edit')
      }
    } catch (err) {
      console.error(err)
    }
  }

  const deleteVersion = async (id: string) => {
    if (!confirm('Delete this version?')) return
    try {
      await api.delete(`/resume-versions/${id}`)
      setVersions(versions.filter(v => v.id !== id))
    } catch (err) {
      console.error(err)
    }
  }

  const toggleSection = (section: string) => {
    if (!resumeData) return
    const current = resumeData.sectionOrder
    const next = current.includes(section)
      ? current.filter(s => s !== section)
      : [...current, section]
    setResumeData({ ...resumeData, sectionOrder: next })
  }

  const moveSection = (section: string, direction: 'up' | 'down') => {
    if (!resumeData) return
    const current = [...resumeData.sectionOrder]
    const idx = current.indexOf(section)
    if (idx === -1) return
    const newIdx = direction === 'up' ? idx - 1 : idx + 1
    if (newIdx < 0 || newIdx >= current.length) return
    const temp = current[idx]
    current[idx] = current[newIdx]
    current[newIdx] = temp
    setResumeData({ ...resumeData, sectionOrder: current })
  }

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  if (!resumeData) {
    return (
      <div className="p-8">
        <h1 className="text-2xl font-bold mb-4">Resume Editor</h1>
        <p className="text-muted-foreground">Could not load resume data. Make sure your profile is complete.</p>
      </div>
    )
  }

  return (
    <div className="p-8 max-w-5xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Resume Editor</h1>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => setActiveTab(activeTab === 'edit' ? 'versions' : 'edit')}>
            {activeTab === 'edit' ? 'Versions' : 'Edit'}
          </Button>
          <Button onClick={saveVersion} disabled={isSaving}>
            {isSaving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save
          </Button>
          <Button onClick={generatePdf} disabled={isGenerating}>
            {isGenerating ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
            Generate PDF
          </Button>
        </div>
      </div>

      {activeTab === 'edit' ? (
        <div className="space-y-6">
          {/* Template & Title */}
          <div className="bg-card border rounded-lg p-6">
            <h2 className="text-lg font-semibold mb-4">Settings</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium mb-2 block">Resume Title</label>
                <input
                  type="text"
                  className="w-full px-3 py-2 border rounded-md bg-background text-sm"
                  value={resumeData.title}
                  onChange={(e: React.ChangeEvent<HTMLInputElement>) => setResumeData({ ...resumeData, title: e.target.value })}
                />
              </div>
              <div>
                <label className="text-sm font-medium mb-2 block">Template</label>
                <select
                  className="w-full px-3 py-2 border rounded-md bg-background text-sm"
                  value={resumeData.template}
                  onChange={(e: React.ChangeEvent<HTMLSelectElement>) => setResumeData({ ...resumeData, template: e.target.value })}
                >
                  <option value="modern">Modern</option>
                  <option value="classic">Classic</option>
                  <option value="minimal">Minimal</option>
                </select>
              </div>
            </div>
          </div>

          {/* Section Order */}
          <div className="bg-card border rounded-lg p-6">
            <h2 className="text-lg font-semibold mb-4">Sections</h2>
            <div className="space-y-2">
              {ALL_SECTIONS.map(section => {
                const isEnabled = resumeData.sectionOrder.includes(section)
                const idx = resumeData.sectionOrder.indexOf(section)
                return (
                  <div key={section} className="flex items-center gap-3">
                    <input
                      type="checkbox"
                      checked={isEnabled}
                      onChange={() => toggleSection(section)}
                      className="h-4 w-4"
                    />
                    <span className={`text-sm capitalize ${isEnabled ? '' : 'text-muted-foreground line-through'}`}>
                      {section}
                    </span>
                    {isEnabled && (
                      <div className="flex gap-1 ml-auto">
                        <Button size="sm" variant="ghost" onClick={() => moveSection(section, 'up')} disabled={idx === 0}>↑</Button>
                        <Button size="sm" variant="ghost" onClick={() => moveSection(section, 'down')} disabled={idx === resumeData.sectionOrder.length - 1}>↓</Button>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          </div>

          {/* Profile Preview */}
          <div className="bg-card border rounded-lg p-6">
            <h2 className="text-lg font-semibold mb-4">Profile Preview</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
              <div><strong>Name:</strong> {resumeData.profile.fullName || '—'}</div>
              <div><strong>Email:</strong> {resumeData.profile.email || '—'}</div>
              <div><strong>Phone:</strong> {resumeData.profile.phone || '—'}</div>
              <div><strong>Location:</strong> {resumeData.profile.location || '—'}</div>
              <div><strong>LinkedIn:</strong> {resumeData.profile.linkedIn || '—'}</div>
              <div><strong>GitHub:</strong> {resumeData.profile.github || '—'}</div>
            </div>
            {resumeData.profile.summary && (
              <div className="mt-4">
                <strong>Summary:</strong>
                <p className="text-sm text-muted-foreground mt-1">{resumeData.profile.summary}</p>
              </div>
            )}
          </div>

          {/* Experience Preview */}
          {resumeData.experience.length > 0 && (
            <div className="bg-card border rounded-lg p-6">
              <h2 className="text-lg font-semibold mb-4">Experience ({resumeData.experience.length})</h2>
              <div className="space-y-3">
                {resumeData.experience.map(exp => (
                  <div key={exp.id} className="text-sm border-b pb-2 last:border-0">
                    <p className="font-medium">{exp.title} — {exp.company}</p>
                    <p className="text-muted-foreground">{exp.startDate} — {exp.isCurrent ? 'Present' : exp.endDate}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Education Preview */}
          {resumeData.education.length > 0 && (
            <div className="bg-card border rounded-lg p-6">
              <h2 className="text-lg font-semibold mb-4">Education ({resumeData.education.length})</h2>
              <div className="space-y-3">
                {resumeData.education.map(edu => (
                  <div key={edu.id} className="text-sm border-b pb-2 last:border-0">
                    <p className="font-medium">{edu.degree} — {edu.school}</p>
                    <p className="text-muted-foreground">{edu.startDate} — {edu.isCurrent ? 'Present' : edu.endDate}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Skills Preview */}
          {resumeData.skills.length > 0 && (
            <div className="bg-card border rounded-lg p-6">
              <h2 className="text-lg font-semibold mb-4">Skills</h2>
              <div className="space-y-2">
                {resumeData.skills.map((group, i) => (
                  <div key={i} className="text-sm">
                    <span className="font-medium">{group.category}:</span>{' '}
                    {group.skills.join(', ')}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Projects Preview */}
          {resumeData.projects.length > 0 && (
            <div className="bg-card border rounded-lg p-6">
              <h2 className="text-lg font-semibold mb-4">Projects ({resumeData.projects.length})</h2>
              <div className="space-y-3">
                {resumeData.projects.map(proj => (
                  <div key={proj.id} className="text-sm border-b pb-2 last:border-0">
                    <p className="font-medium">{proj.name}</p>
                    {proj.technologies && <p className="text-muted-foreground">{proj.technologies}</p>}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Certifications Preview */}
          {resumeData.certifications.length > 0 && (
            <div className="bg-card border rounded-lg p-6">
              <h2 className="text-lg font-semibold mb-4">Certifications ({resumeData.certifications.length})</h2>
              <div className="space-y-1">
                {resumeData.certifications.map((cert, i) => (
                  <div key={i} className="text-sm">
                    {cert.name} — {cert.organization}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      ) : (
        <div className="space-y-4">
          {versions.length === 0 ? (
            <p className="text-muted-foreground">No saved versions yet.</p>
          ) : (
            versions.map(v => (
              <div key={v.id} className="bg-card border rounded-lg p-4 flex items-center justify-between">
                <div>
                  <p className="font-medium">{v.title}</p>
                  <p className="text-sm text-muted-foreground">{v.template} — {new Date(v.createdAt).toLocaleDateString()}</p>
                </div>
                <div className="flex gap-2">
                  <Button size="sm" variant="outline" onClick={() => loadVersion(v.id)}>
                    <FileText className="mr-2 h-4 w-4" />
                    Load
                  </Button>
                  <Button size="sm" variant="ghost" className="text-destructive" onClick={() => deleteVersion(v.id)}>
                    Delete
                  </Button>
                </div>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  )
}
