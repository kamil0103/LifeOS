import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, ExternalLink } from 'lucide-react'

interface WorkExp {
  id: string
  company: string
  title: string
  location?: string
  startDate?: string
  endDate?: string
  isCurrent: boolean
  bullets?: string
}

interface Project {
  id: string
  name: string
  description?: string
  technologies?: string
  link?: string
  startDate?: string
  endDate?: string
  isCurrent: boolean
  isPortfolioProject: boolean
}

export default function ExperiencePage() {
  const [experiences, setExperiences] = useState<WorkExp[]>([])
  const [projects, setProjects] = useState<Project[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [activeTab, setActiveTab] = useState<'work' | 'projects'>('work')

  const [showAddWork, setShowAddWork] = useState(false)
  const [newWork, setNewWork] = useState({
    company: '', title: '', location: '', startDate: '', endDate: '', isCurrent: false, bullets: ''
  })

  const [showAddProj, setShowAddProj] = useState(false)
  const [newProj, setNewProj] = useState({
    name: '', description: '', technologies: '', link: '', startDate: '', endDate: '', isCurrent: false, isPortfolioProject: false
  })

  useEffect(() => {
    loadData()
  }, [])

  const loadData = () => {
    setIsLoading(true)
    Promise.all([
      api.get('/experience/work').then(({ data }) => setExperiences(data)).catch(() => {}),
      api.get('/experience/projects').then(({ data }) => setProjects(data)).catch(() => {}),
    ]).finally(() => setIsLoading(false))
  }

  const addWork = async () => {
    if (!newWork.company || !newWork.title) return
    await api.post('/experience/work', newWork)
    setNewWork({ company: '', title: '', location: '', startDate: '', endDate: '', isCurrent: false, bullets: '' })
    setShowAddWork(false)
    loadData()
  }

  const addProject = async () => {
    if (!newProj.name) return
    await api.post('/experience/projects', newProj)
    setNewProj({ name: '', description: '', technologies: '', link: '', startDate: '', endDate: '', isCurrent: false, isPortfolioProject: false })
    setShowAddProj(false)
    loadData()
  }

  const deleteWork = async (id: string) => {
    if (!confirm('Delete this work experience?')) return
    await api.delete(`/experience/work/${id}`)
    loadData()
  }

  const deleteProject = async (id: string) => {
    if (!confirm('Delete this project?')) return
    await api.delete(`/experience/projects/${id}`)
    loadData()
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
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">Experience</h1>
        <p className="text-muted-foreground mt-1">Work history and projects</p>
      </div>

      <div className="flex gap-4 mb-6">
        <button
          onClick={() => setActiveTab('work')}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            activeTab === 'work' ? 'bg-primary text-primary-foreground' : 'bg-secondary hover:bg-secondary/80'
          }`}
        >
          Work Experience
        </button>
        <button
          onClick={() => setActiveTab('projects')}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            activeTab === 'projects' ? 'bg-primary text-primary-foreground' : 'bg-secondary hover:bg-secondary/80'
          }`}
        >
          Projects
        </button>
      </div>

      {activeTab === 'work' && (
        <div className="space-y-6">
          <div className="flex justify-end">
            <Button onClick={() => setShowAddWork(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add Experience
            </Button>
          </div>

          {showAddWork && (
            <div className="bg-card border rounded-lg p-6 shadow-sm">
              <h3 className="text-lg font-semibold mb-4">Add Work Experience</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <input placeholder="Company" value={newWork.company} onChange={(e) => setNewWork({ ...newWork, company: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Job Title" value={newWork.title} onChange={(e) => setNewWork({ ...newWork, title: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Location" value={newWork.location} onChange={(e) => setNewWork({ ...newWork, location: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <div className="flex items-center gap-2">
                  <input type="checkbox" checked={newWork.isCurrent} onChange={(e) => setNewWork({ ...newWork, isCurrent: e.target.checked })} />
                  <span className="text-sm">Current position</span>
                </div>
                <input placeholder="Start Date (YYYY-MM)" value={newWork.startDate} onChange={(e) => setNewWork({ ...newWork, startDate: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="End Date (YYYY-MM)" value={newWork.endDate} onChange={(e) => setNewWork({ ...newWork, endDate: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <textarea placeholder="Bullet points (one per line)" value={newWork.bullets} onChange={(e) => setNewWork({ ...newWork, bullets: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm min-h-[80px] md:col-span-2" />
              </div>
              <div className="mt-4 flex gap-2">
                <Button onClick={addWork}>Save</Button>
                <Button variant="outline" onClick={() => setShowAddWork(false)}>Cancel</Button>
              </div>
            </div>
          )}

          {experiences.length === 0 && (
            <div className="text-center py-12 text-muted-foreground">No work experience yet.</div>
          )}

          {experiences.map((exp) => (
            <div key={exp.id} className="bg-card border rounded-lg p-6 shadow-sm">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="font-semibold">{exp.title}</h3>
                  <p className="text-sm text-muted-foreground">{exp.company} {exp.location && `· ${exp.location}`}</p>
                  <p className="text-sm text-muted-foreground">
                    {exp.startDate} — {exp.isCurrent ? 'Present' : exp.endDate}
                  </p>
                </div>
                <Button variant="ghost" size="sm" onClick={() => deleteWork(exp.id)}>
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              </div>
              {exp.bullets && (
                <div className="mt-3 text-sm whitespace-pre-line text-muted-foreground">{exp.bullets}</div>
              )}
            </div>
          ))}
        </div>
      )}

      {activeTab === 'projects' && (
        <div className="space-y-6">
          <div className="flex justify-end">
            <Button onClick={() => setShowAddProj(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add Project
            </Button>
          </div>

          {showAddProj && (
            <div className="bg-card border rounded-lg p-6 shadow-sm">
              <h3 className="text-lg font-semibold mb-4">Add Project</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <input placeholder="Project Name" value={newProj.name} onChange={(e) => setNewProj({ ...newProj, name: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Technologies (comma separated)" value={newProj.technologies} onChange={(e) => setNewProj({ ...newProj, technologies: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Project Link" value={newProj.link} onChange={(e) => setNewProj({ ...newProj, link: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <div className="flex items-center gap-4">
                  <div className="flex items-center gap-2">
                    <input type="checkbox" checked={newProj.isCurrent} onChange={(e) => setNewProj({ ...newProj, isCurrent: e.target.checked })} />
                    <span className="text-sm">Ongoing</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <input type="checkbox" checked={newProj.isPortfolioProject} onChange={(e) => setNewProj({ ...newProj, isPortfolioProject: e.target.checked })} />
                    <span className="text-sm">Portfolio</span>
                  </div>
                </div>
                <input placeholder="Start Date (YYYY-MM)" value={newProj.startDate} onChange={(e) => setNewProj({ ...newProj, startDate: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="End Date (YYYY-MM)" value={newProj.endDate} onChange={(e) => setNewProj({ ...newProj, endDate: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <textarea placeholder="Description" value={newProj.description} onChange={(e) => setNewProj({ ...newProj, description: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm min-h-[80px] md:col-span-2" />
              </div>
              <div className="mt-4 flex gap-2">
                <Button onClick={addProject}>Save</Button>
                <Button variant="outline" onClick={() => setShowAddProj(false)}>Cancel</Button>
              </div>
            </div>
          )}

          {projects.length === 0 && (
            <div className="text-center py-12 text-muted-foreground">No projects yet.</div>
          )}

          {projects.map((proj) => (
            <div key={proj.id} className="bg-card border rounded-lg p-6 shadow-sm">
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <h3 className="font-semibold">{proj.name}</h3>
                    {proj.isPortfolioProject && <span className="text-xs bg-primary/10 text-primary px-2 py-0.5 rounded">Portfolio</span>}
                  </div>
                  <p className="text-sm text-muted-foreground">
                    {proj.startDate} — {proj.isCurrent ? 'Present' : proj.endDate}
                  </p>
                  {proj.technologies && (
                    <p className="text-sm text-muted-foreground mt-1">{proj.technologies}</p>
                  )}
                </div>
                <div className="flex gap-2">
                  {proj.link && (
                    <a href={proj.link} target="_blank" rel="noopener noreferrer">
                      <ExternalLink className="h-4 w-4 text-muted-foreground hover:text-primary" />
                    </a>
                  )}
                  <Button variant="ghost" size="sm" onClick={() => deleteProject(proj.id)}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              </div>
              {proj.description && <p className="mt-3 text-sm text-muted-foreground">{proj.description}</p>}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
