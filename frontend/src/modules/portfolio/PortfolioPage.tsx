import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, ExternalLink, Github, Globe, Sparkles } from 'lucide-react'

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
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const fetchGitHubRepos = async () => {
    if (!githubUsername.trim()) return
    setIsImporting(true)
    try {
      const { data } = await api.get(`/portfolio/github/${githubUsername}/repos`)
      setGithubRepos(data)
    } catch (err) {
      console.error(err)
      alert('Could not fetch GitHub repos')
    } finally {
      setIsImporting(false)
    }
  }

  const importRepo = async (repo: GitHubRepo) => {
    try {
      // Create as project first via experience/projects endpoint, then update portfolio fields
      const { data: newProject } = await api.post('/experience/projects', {
        name: repo.name,
        description: repo.description || '',
        technologies: repo.language || '',
        link: repo.htmlUrl || '',
        isCurrent: true
      })

      await api.put(`/portfolio/projects/${newProject.id}`, {
        isPortfolioProject: true,
        gitHubRepoUrl: repo.htmlUrl,
        isFeatured: false
      })

      loadPortfolio()
      setGithubRepos(prev => prev.filter(r => r.name !== repo.name))
    } catch (err) {
      console.error(err)
    }
  }

  const toggleFeatured = async (id: string) => {
    const project = projects.find(p => p.id === id)
    if (!project) return
    try {
      await api.put(`/portfolio/projects/${id}`, {
        ...project,
        isFeatured: !project.isFeatured,
        isPortfolioProject: true
      })
      loadPortfolio()
    } catch (err) {
      console.error(err)
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
    <div className="p-8 max-w-6xl mx-auto">
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
                <Button size="sm" variant="outline" onClick={() => importRepo(repo)}>
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
                <div className="flex gap-1">
                  {project.isFeatured && <Sparkles className="h-4 w-4 text-primary" />}
                  <button onClick={() => toggleFeatured(project.id)} className="text-xs text-muted-foreground hover:text-primary">
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
              <div className="flex gap-2 mt-auto">
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
    </div>
  )
}
