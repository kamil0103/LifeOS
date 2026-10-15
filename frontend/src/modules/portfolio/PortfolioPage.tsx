import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { useRecordActions } from '@/hooks/useRecordActions'
import { useUnsavedChanges } from '@/hooks/useUnsavedChanges'
import { Button } from '@/components/ui/button'
import { Loader2, ExternalLink, Github, Globe, Sparkles, Pencil } from 'lucide-react'

interface PortfolioProject {
  id: string
  name: string
  description?: string
  technologies?: string
  link?: string
  startDate?: string
  endDate?: string
  isCurrent: boolean
  gitHubRepoUrl?: string
  deployedUrl?: string
  screenshotUrl?: string
  isFeatured: boolean
  gitHubStars?: number
}

interface GitHubRepo {
  name: string
  description?: string
  stars: number
  language?: string
  htmlUrl?: string
}

export default function PortfolioPage() {
  const { busy, error, setError, mutate } = useRecordActions()
  const [projects, setProjects] = useState<PortfolioProject[]>([])
  const [githubRepos, setGithubRepos] = useState<GitHubRepo[]>([])
  const [githubUsername, setGithubUsername] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isImporting, setIsImporting] = useState(false)

  useEffect(() => {
    loadPortfolio()
  }, [])

  const loadPortfolio = async () => {
    setIsLoading(true)
    try {
      const { data } = await api.get('/portfolio')
      setProjects(data)
    } catch (err) {
      setError('Could not load portfolio projects. Try again.')
    } finally {
      setIsLoading(false)
    }
  }

  const fetchGitHubRepos = async () => {
    if (!githubUsername.trim()) return
    setIsImporting(true)
    try {
      const { data } = await api.get(`/portfolio/github/${encodeURIComponent(githubUsername.trim())}/repos`)
      setGithubRepos(data)
    } catch (err) {
      console.error(err)
      setError((err as any)?.response?.data?.detail || 'Could not fetch GitHub repositories. Try again.')
    } finally {
      setIsImporting(false)
    }
  }

  const importRepo = (repo: GitHubRepo) => mutate(async () => {
    await api.post('/portfolio/import', { repoUrl: repo.htmlUrl })
    setGithubRepos(prev => prev.filter(r => r.htmlUrl !== repo.htmlUrl))
    await loadPortfolio()
  })

  const [editProject, setEditProject] = useState<PortfolioProject | null>(null)

  const saveEditProject = () => mutate(async () => {
    if (!editProject?.name.trim()) return
    await api.put(`/portfolio/projects/${editProject.id}/details`, { ...editProject, isPortfolioProject: true })
    setEditProject(null); await loadPortfolio()
  })
  const toggleFeatured = (id: string) => mutate(async () => {
    const project = projects.find(p => p.id === id); if (!project) return
    await api.put(`/portfolio/projects/${id}`, { ...project, isFeatured: !project.isFeatured, isPortfolioProject: true })
    await loadPortfolio()
  })
  const syncProject = (id: string) => mutate(async () => {
    await api.post(`/portfolio/projects/${id}/sync-github`)
    await loadPortfolio()
  })
  useUnsavedChanges(!!editProject && JSON.stringify(editProject) !== JSON.stringify(projects.find(p => p.id === editProject.id)))

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-6xl mx-auto">
      {error && <div role="alert" className="fixed top-4 left-1/2 -translate-x-1/2 z-[70] w-[min(90vw,40rem)] bg-card border border-destructive rounded p-3 text-sm">{error} <button className="underline" onClick={loadPortfolio}>Retry loading</button></div>}
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Portfolio</h1>
      </div>

      {/* GitHub Import */}
      <div className="bg-card border rounded-lg p-6 mb-6">
        <h2 className="text-lg font-semibold mb-4">Import from GitHub</h2>
        <div className="flex gap-2">
          <input
            type="text"
            placeholder="GitHub username"
            className="flex-1 px-3 py-2 border rounded-md bg-background text-sm"
            value={githubUsername}
            onChange={e => setGithubUsername(e.target.value)}
          />
          <Button onClick={fetchGitHubRepos} disabled={isImporting}>
            {isImporting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Github className="mr-2 h-4 w-4" />}
            Fetch
          </Button>
        </div>

        {githubRepos.length > 0 && (
          <div className="mt-4 space-y-2">
            <p className="text-sm text-muted-foreground">Click to import:</p>
            {githubRepos.map(repo => (
              <div key={repo.name} className="flex items-center justify-between bg-secondary/30 rounded-md p-3">
                <div>
                  <p className="font-medium">{repo.name}</p>
                  <p className="text-xs text-muted-foreground">{repo.description || 'No description'} · {repo.language || 'Unknown'} · {repo.stars} stars</p>
                </div>
                <Button size="sm" variant="outline" disabled={busy} onClick={() => importRepo(repo)}>
                  <Sparkles className="mr-2 h-4 w-4" />
                  Import
                </Button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Projects Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {projects.length === 0 ? (
          <div className="col-span-2 bg-card border rounded-lg p-8 text-center">
            <Globe className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-muted-foreground">No portfolio projects yet.</p>
            <p className="text-sm text-muted-foreground mt-1">Import from GitHub or mark existing projects as portfolio.</p>
          </div>
        ) : (
          projects.map(project => (
            <div key={project.id} className={`bg-card border rounded-lg p-6 ${project.isFeatured ? 'border-primary/30' : ''}`}>
              <div className="flex items-start justify-between mb-2">
                <h3 className="font-semibold">{project.name}</h3>
                <div className="flex gap-1 items-center">
                  {project.isFeatured && <Sparkles className="h-4 w-4 text-primary" />}
                  <button aria-label={`Edit ${project.name}`} onClick={() => setEditProject(project)} className="text-muted-foreground hover:text-primary">
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button disabled={busy} onClick={() => toggleFeatured(project.id)} className="text-xs text-muted-foreground hover:text-primary">
                    {project.isFeatured ? 'Unfeature' : 'Feature'}
                  </button>
                </div>
              </div>
              {project.description && <p className="text-sm text-muted-foreground mb-3">{project.description}</p>}
              {project.technologies && (
                <div className="flex flex-wrap gap-1 mb-3">
                  {project.technologies.split(',').map((tech, i) => (
                    <span key={i} className="text-xs bg-secondary px-2 py-1 rounded">{tech.trim()}</span>
                  ))}
                </div>
              )}
              {project.gitHubRepoUrl && <Button size="sm" variant="outline" disabled={busy} onClick={() => syncProject(project.id)}>Refresh GitHub stats</Button>}
              <div className="flex gap-2 mt-3">
                {project.gitHubRepoUrl && (
                  <a href={project.gitHubRepoUrl} target="_blank" rel="noopener noreferrer" className="text-xs flex items-center gap-1 text-muted-foreground hover:text-primary">
                    <Github className="h-3 w-3" />
                    {project.gitHubStars !== null && project.gitHubStars !== undefined ? `${project.gitHubStars} stars` : 'GitHub'}
                  </a>
                )}
                {project.deployedUrl && (
                  <a href={project.deployedUrl} target="_blank" rel="noopener noreferrer" className="text-xs flex items-center gap-1 text-muted-foreground hover:text-primary">
                    <Globe className="h-3 w-3" />
                    Live
                  </a>
                )}
                {project.link && (
                  <a href={project.link} target="_blank" rel="noopener noreferrer" className="text-xs flex items-center gap-1 text-muted-foreground hover:text-primary">
                    <ExternalLink className="h-3 w-3" />
                    Link
                  </a>
                )}
              </div>
            </div>
          ))
        )}
      </div>

      {/* Edit Project Modal */}
      {editProject && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setEditProject(null)}>
          <div className="bg-card border rounded-lg p-6 w-full max-w-[560px] mx-4 max-h-[85vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h3 className="text-lg font-semibold mb-4">Edit Portfolio Project</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <input placeholder="Project Name" value={editProject.name} onChange={e => setEditProject({ ...editProject, name: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Technologies (comma separated)" value={editProject.technologies ?? ''} onChange={e => setEditProject({ ...editProject, technologies: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="GitHub Repo URL" value={editProject.gitHubRepoUrl ?? ''} onChange={e => setEditProject({ ...editProject, gitHubRepoUrl: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Deployed URL" value={editProject.deployedUrl ?? ''} onChange={e => setEditProject({ ...editProject, deployedUrl: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Other Link" value={editProject.link ?? ''} onChange={e => setEditProject({ ...editProject, link: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Screenshot URL" value={editProject.screenshotUrl ?? ''} onChange={e => setEditProject({ ...editProject, screenshotUrl: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input aria-label="Project start date" placeholder="Start date (YYYY-MM)" value={editProject.startDate ?? ''} onChange={e => setEditProject({ ...editProject, startDate: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input aria-label="Project end date" disabled={editProject.isCurrent} placeholder="End date (YYYY-MM)" value={editProject.endDate ?? ''} onChange={e => setEditProject({ ...editProject, endDate: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={editProject.isCurrent} onChange={e => setEditProject({ ...editProject, isCurrent: e.target.checked })} />Ongoing project</label>
              <textarea placeholder="Description" value={editProject.description ?? ''} onChange={e => setEditProject({ ...editProject, description: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm min-h-[100px] md:col-span-2" />
            </div>
            <div className="mt-4 flex gap-2 justify-end">
              <Button variant="outline" onClick={() => setEditProject(null)}>Cancel</Button>
              <Button disabled={busy} onClick={saveEditProject}>Save</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
