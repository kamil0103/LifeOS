import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, ExternalLink, Sparkles, Search, Save, Rss, Target, MessageSquare } from 'lucide-react'

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
  const [activeTab, setActiveTab] = useState<'saved' | 'discover' | 'rss'>('saved')
  const [savedJobs, setSavedJobs] = useState<Job[]>([])
  const [discoveredJobs, setDiscoveredJobs] = useState<ExternalJob[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isDiscovering, setIsDiscovering] = useState(false)
  const [searchKeywords, setSearchKeywords] = useState('software engineer')
  const [searchLocation, setSearchLocation] = useState('')
  const [analyzingId, setAnalyzingId] = useState<string | null>(null)
  const [matchResult, setMatchResult] = useState<any | null>(null)
  const [showMatchModal, setShowMatchModal] = useState(false)
  const [interviewQa, setInterviewQa] = useState<any | null>(null)
  const [showQaModal, setShowQaModal] = useState(false)
  const [rssUrl, setRssUrl] = useState('')
  const [rssJobs, setRssJobs] = useState<any[]>([])
  const [isFetchingRss, setIsFetchingRss] = useState(false)

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

  const analyzeMatch = async (jobId: string) => {
    setAnalyzingId(jobId)
    try {
      const { data } = await api.post('/jobmatch/analyze', { jobId })
      setMatchResult(data)
      setShowMatchModal(true)
    } catch (err) {
      console.error(err)
      alert('Match analysis failed')
    } finally {
      setAnalyzingId(null)
    }
  }

  const generateInterviewQa = async (jobId: string) => {
    setAnalyzingId(jobId)
    try {
      const { data } = await api.post('/jobmatch/interview-qa', { jobId })
      setInterviewQa(data)
      setShowQaModal(true)
    } catch (err) {
      console.error(err)
      alert('Interview Q&A generation failed')
    } finally {
      setAnalyzingId(null)
    }
  }

  const fetchRss = async () => {
    if (!rssUrl.trim()) return
    setIsFetchingRss(true)
    try {
      const { data } = await api.get(`/jobdiscovery/rss?url=${encodeURIComponent(rssUrl)}`)
      setRssJobs(data)
    } catch (err) {
      console.error(err)
      alert('RSS fetch failed')
    } finally {
      setIsFetchingRss(false)
    }
  }

  const importRssJob = async (job: any) => {
    try {
      await api.post('/jobdiscovery/rss/import', {
        title: job.title,
        description: job.description,
        link: job.link,
        company: job.company
      })
      alert('Job imported!')
      setRssJobs(prev => prev.filter(j => j.link !== job.link))
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
        <button
          onClick={() => setActiveTab('rss')}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            activeTab === 'rss' ? 'bg-primary text-primary-foreground' : 'bg-secondary hover:bg-secondary/80'
          }`}
        >
          <Rss className="inline h-4 w-4 mr-1" />
          RSS
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
                  <button onClick={() => analyzeMatch(job.id)} disabled={analyzingId === job.id} title="Match Analysis">
                    <Target className={`h-4 w-4 ${analyzingId === job.id ? 'animate-pulse text-primary' : 'text-muted-foreground hover:text-primary'}`} />
                  </button>
                  <button onClick={() => generateInterviewQa(job.id)} disabled={analyzingId === job.id} title="Interview Q&A">
                    <MessageSquare className={`h-4 w-4 ${analyzingId === job.id ? 'animate-pulse text-primary' : 'text-muted-foreground hover:text-primary'}`} />
                  </button>
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

      {activeTab === 'rss' && (
        <div className="space-y-6">
          <div className="bg-card border rounded-lg p-6 shadow-sm">
            <h3 className="font-semibold mb-2">RSS Job Feed</h3>
            <div className="flex gap-2">
              <input
                type="text"
                placeholder="https://example.com/jobs.rss"
                className="flex-1 px-3 py-2 border rounded-md bg-background text-sm"
                value={rssUrl}
                onChange={e => setRssUrl(e.target.value)}
              />
              <Button onClick={fetchRss} disabled={isFetchingRss}>
                {isFetchingRss ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Rss className="mr-2 h-4 w-4" />}
                Fetch
              </Button>
            </div>
          </div>

          <div className="space-y-4">
            {rssJobs.map((job, idx) => (
              <div key={idx} className="bg-card border rounded-lg p-6 shadow-sm">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <h3 className="font-semibold">{job.title}</h3>
                    <p className="text-sm text-muted-foreground">{job.company} {job.publishedAt && `· ${job.publishedAt}`}</p>
                    <p className="text-sm text-muted-foreground mt-2 line-clamp-3">{job.description}</p>
                  </div>
                  <div className="flex gap-2 ml-4">
                    <Button size="sm" variant="outline" onClick={() => importRssJob(job)}>
                      <Save className="mr-2 h-4 w-4" />
                      Import
                    </Button>
                    {job.link && (
                      <a href={job.link} target="_blank" rel="noopener noreferrer">
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

      {/* Match Analysis Modal */}
      {showMatchModal && matchResult && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setShowMatchModal(false)}>
          <div className="bg-card border rounded-lg p-6 w-[600px] max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h2 className="text-xl font-semibold mb-4">Match Analysis</h2>
            <div className="text-center mb-4">
              <p className="text-4xl font-bold text-primary">{matchResult.matchScore}%</p>
              <p className="text-sm text-muted-foreground">Match Score</p>
            </div>
            <p className="text-sm mb-4">{matchResult.summary}</p>
            {matchResult.matchedSkills?.length > 0 && (
              <div className="mb-3">
                <h3 className="text-sm font-medium text-green-500">Matched Skills</h3>
                <p className="text-sm">{matchResult.matchedSkills.join(', ')}</p>
              </div>
            )}
            {matchResult.missingSkills?.length > 0 && (
              <div className="mb-3">
                <h3 className="text-sm font-medium text-yellow-500">Missing Skills</h3>
                <p className="text-sm">{matchResult.missingSkills.join(', ')}</p>
              </div>
            )}
            {matchResult.suggestedImprovements?.length > 0 && (
              <div className="mb-3">
                <h3 className="text-sm font-medium">Suggestions</h3>
                <ul className="text-sm list-disc pl-4">
                  {matchResult.suggestedImprovements.map((s: string, i: number) => <li key={i}>{s}</li>)}
                </ul>
              </div>
            )}
            <Button onClick={() => setShowMatchModal(false)}>Close</Button>
          </div>
        </div>
      )}

      {/* Interview Q&A Modal */}
      {showQaModal && interviewQa && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setShowQaModal(false)}>
          <div className="bg-card border rounded-lg p-6 w-[700px] max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h2 className="text-xl font-semibold mb-2">Interview Preparation</h2>
            <p className="text-sm text-muted-foreground mb-4">{interviewQa.roleFocus}</p>
            <p className="text-sm mb-4">{interviewQa.preparationTips}</p>
            <div className="space-y-4">
              {interviewQa.questions?.map((q: any, i: number) => (
                <div key={i} className="border rounded-lg p-4">
                  <span className="text-xs bg-secondary px-2 py-0.5 rounded mb-2 inline-block">{q.category}</span>
                  <p className="font-medium mb-2">{q.question}</p>
                  {q.suggestedAnswer && (
                    <div className="bg-secondary/30 rounded p-3 mt-2">
                      <p className="text-xs text-muted-foreground mb-1">Suggested Answer:</p>
                      <p className="text-sm">{q.suggestedAnswer}</p>
                    </div>
                  )}
                  {q.keyPoints && <p className="text-xs text-muted-foreground mt-2">Key: {q.keyPoints}</p>}
                </div>
              ))}
            </div>
            <Button className="mt-4" onClick={() => setShowQaModal(false)}>Close</Button>
          </div>
        </div>
      )}
    </div>
  )
}
