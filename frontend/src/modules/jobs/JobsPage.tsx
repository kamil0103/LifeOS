import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, ExternalLink, Sparkles, Search, Save } from 'lucide-react'

interface Job {
  id: string
  title: string
  company: string
  location?: string
  description: string
  url?: string
  source: string
  salaryRange?: string
  jobType?: string
  postedDate?: string
  matchScore?: number
  status: string
  createdAt: string
}

interface ExternalJob {
  title: string
  company: string
  location?: string
  description: string
  url?: string
  source: string
  salaryRange?: string
  jobType?: string
  postedDate?: string
}

export default function JobsPage() {
  const [activeTab, setActiveTab] = useState<'saved' | 'discover'>('saved')
  const [savedJobs, setSavedJobs] = useState<Job[]>([])
  const [discoveredJobs, setDiscoveredJobs] = useState<ExternalJob[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isDiscovering, setIsDiscovering] = useState(false)
  const [searchKeywords, setSearchKeywords] = useState('software engineer')
  const [searchLocation, setSearchLocation] = useState('')
  const [analyzingId, setAnalyzingId] = useState<string | null>(null)

  useEffect(() => {
    if (activeTab === 'saved') {
      loadSavedJobs()
    }
  }, [activeTab])

  const loadSavedJobs = () => {
    setIsLoading(true)
    api.get('/jobs')
      .then(({ data }) => setSavedJobs(data))
      .catch(console.error)
      .finally(() => setIsLoading(false))
  }

  const discoverJobs = async () => {
    setIsDiscovering(true)
    try {
      const { data } = await api.get('/jobs/discover', {
        params: { keywords: searchKeywords, location: searchLocation }
      })
      setDiscoveredJobs(data)
    } catch (err) {
      console.error(err)
    } finally {
      setIsDiscovering(false)
    }
  }

  const saveJob = async (job: ExternalJob) => {
    try {
      await api.post('/jobs', {
        title: job.title,
        company: job.company,
        location: job.location,
        description: job.description,
        url: job.url,
        source: job.source,
        salaryRange: job.salaryRange,
        jobType: job.jobType,
        postedDate: job.postedDate
      })
      alert('Job saved!')
    } catch (err) {
      console.error(err)
    }
  }

  const deleteJob = async (id: string) => {
    if (!confirm('Delete this job?')) return
    try {
      await api.delete(`/jobs/${id}`)
      loadSavedJobs()
    } catch (err) {
      console.error(err)
    }
  }

  const analyzeJob = async (id: string) => {
    setAnalyzingId(id)
    try {
      await api.post(`/jobs/${id}/analyze`)
      loadSavedJobs()
    } catch (err) {
      console.error(err)
      alert('AI analysis failed. Make sure GEMINI_API_KEY is configured.')
    } finally {
      setAnalyzingId(null)
    }
  }

  const applyToJob = async (jobId: string) => {
    try {
      await api.post('/applications', { jobId })
      alert('Application tracked!')
      loadSavedJobs()
    } catch (err) {
      console.error(err)
    }
  }

  if (isLoading && activeTab === 'saved') {
    return (
      <div className="p-8 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-6xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">Jobs</h1>
        <p className="text-muted-foreground mt-1">Discover and track job opportunities</p>
      </div>

      <div className="flex gap-4 mb-6">
        <button
          onClick={() => setActiveTab('saved')}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            activeTab === 'saved' ? 'bg-primary text-primary-foreground' : 'bg-secondary hover:bg-secondary/80'
          }`}
        >
          Saved Jobs ({savedJobs.length})
        </button>
        <button
          onClick={() => setActiveTab('discover')}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            activeTab === 'discover' ? 'bg-primary text-primary-foreground' : 'bg-secondary hover:bg-secondary/80'
          }`}
        >
          Discover
        </button>
      </div>

      {activeTab === 'saved' && (
        <div className="space-y-4">
          {savedJobs.length === 0 && (
            <div className="text-center py-12 text-muted-foreground">
              No saved jobs yet. Go to Discover to find opportunities.
            </div>
          )}

          {savedJobs.map((job) => (
            <div key={job.id} className="bg-card border rounded-lg p-6 shadow-sm">
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-semibold text-lg">{job.title}</h3>
                    <span className={`text-xs px-2 py-0.5 rounded ${
                      job.status === 'saved' ? 'bg-secondary' :
                      job.status === 'applied' ? 'bg-primary/10 text-primary' :
                      job.status === 'interview' ? 'bg-yellow-500/10 text-yellow-500' :
                      job.status === 'offer' ? 'bg-green-500/10 text-green-500' :
                      'bg-secondary'
                    }`}>
                      {job.status}
                    </span>
                    {job.matchScore && (
                      <span className={`text-xs px-2 py-0.5 rounded ${
                        job.matchScore >= 80 ? 'bg-green-500/10 text-green-500' :
                        job.matchScore >= 60 ? 'bg-yellow-500/10 text-yellow-500' :
                        'bg-destructive/10 text-destructive'
                      }`}>
                        Match: {job.matchScore}%
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-muted-foreground">{job.company} {job.location && `· ${job.location}`}</p>
                  {job.salaryRange && <p className="text-sm text-muted-foreground">{job.salaryRange}</p>}
                  <p className="text-sm text-muted-foreground mt-2 line-clamp-2">{job.description}</p>
                </div>
                <div className="flex gap-2 ml-4">
                  {job.url && (
                    <a href={job.url} target="_blank" rel="noopener noreferrer">
                      <ExternalLink className="h-4 w-4 text-muted-foreground hover:text-primary" />
                    </a>
                  )}
                  <button onClick={() => analyzeJob(job.id)} disabled={analyzingId === job.id}>
                    <Sparkles className={`h-4 w-4 ${analyzingId === job.id ? 'animate-pulse text-primary' : 'text-muted-foreground hover:text-primary'}`} />
                  </button>
                  {job.status === 'saved' && (
                    <button onClick={() => applyToJob(job.id)}>
                      <Plus className="h-4 w-4 text-muted-foreground hover:text-primary" />
                    </button>
                  )}
                  <button onClick={() => deleteJob(job.id)}>
                    <Trash2 className="h-4 w-4 text-destructive hover:text-destructive/80" />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {activeTab === 'discover' && (
        <div className="space-y-6">
          <div className="bg-card border rounded-lg p-6 shadow-sm">
            <div className="flex gap-4">
              <div className="flex-1">
                <label className="text-sm font-medium">Keywords</label>
                <div className="relative mt-1">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <input
                    type="text"
                    value={searchKeywords}
                    onChange={(e) => setSearchKeywords(e.target.value)}
                    className="w-full pl-10 pr-3 py-2 rounded-md border bg-background text-sm"
                    placeholder="Software Engineer, React, etc."
                  />
                </div>
              </div>
              <div className="flex-1">
                <label className="text-sm font-medium">Location</label>
                <input
                  type="text"
                  value={searchLocation}
                  onChange={(e) => setSearchLocation(e.target.value)}
                  className="w-full mt-1 px-3 py-2 rounded-md border bg-background text-sm"
                  placeholder="Remote, Los Angeles, etc."
                />
              </div>
              <div className="flex items-end">
                <Button onClick={discoverJobs} disabled={isDiscovering}>
                  {isDiscovering ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Search className="mr-2 h-4 w-4" />
                  )}
                  Search
                </Button>
              </div>
            </div>
          </div>

          {discoveredJobs.length === 0 && !isDiscovering && (
            <div className="text-center py-12 text-muted-foreground">
              Click Search to discover jobs from external sources.
            </div>
          )}

          <div className="space-y-4">
            {discoveredJobs.map((job, idx) => (
              <div key={idx} className="bg-card border rounded-lg p-6 shadow-sm">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <h3 className="font-semibold">{job.title}</h3>
                    <p className="text-sm text-muted-foreground">{job.company} {job.location && `· ${job.location}`}</p>
                    <span className="text-xs bg-secondary px-2 py-0.5 rounded">{job.source}</span>
                    <p className="text-sm text-muted-foreground mt-2 line-clamp-3">{job.description}</p>
                  </div>
                  <div className="flex gap-2 ml-4">
                    <Button size="sm" variant="outline" onClick={() => saveJob(job)}>
                      <Save className="mr-2 h-4 w-4" />
                      Save
                    </Button>
                    {job.url && (
                      <a href={job.url} target="_blank" rel="noopener noreferrer">
                        <ExternalLink className="h-4 w-4 text-muted-foreground hover:text-primary mt-2" />
                      </a>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
