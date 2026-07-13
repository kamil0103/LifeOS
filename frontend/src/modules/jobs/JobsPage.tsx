import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, ExternalLink, Sparkles, Search, Save, Rss, Target, MessageSquare, X, Briefcase } from 'lucide-react'

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
  
  // Manual job creation
  const [showAddJob, setShowAddJob] = useState(false)
  const [newJob, setNewJob] = useState({
    title: '',
    company: '',
    location: '',
    description: '',
    url: '',
    salaryRange: '',
    jobType: 'full_time',
    source: 'manual'
  })
  const [jobError, setJobError] = useState<string | null>(null)

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

  const createJob = async () => {
    if (!newJob.title.trim() || !newJob.company.trim()) {
      setJobError('Title and Company are required')
      return
    }
    setJobError(null)
    try {
      await api.post('/jobs', newJob)
      setNewJob({ title: '', company: '', location: '', description: '', url: '', salaryRange: '', jobType: 'full_time', source: 'manual' })
      setShowAddJob(false)
      loadSavedJobs()
    } catch (err: any) {
      setJobError(err.response?.data?.detail || 'Failed to create job')
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
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="h-10 w-10 rounded-full border-2 border-blue-500/30 border-t-blue-500 animate-spin" />
      </div>
    )
  }

  return (
    <div className="max-w-6xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-header">Jobs</h1>
          <p className="text-white/40 mt-1 text-sm">Discover, create, and track job opportunities</p>
        </div>
        <Button onClick={() => { setShowAddJob(true); setActiveTab('saved') }} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">
          <Briefcase className="mr-2 h-4 w-4" />
          Add Job Manually
        </Button>
      </div>

      <div className="flex gap-2">
        {['saved', 'discover', 'rss'].map((tab) => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab as any)}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all ${
              activeTab === tab 
                ? 'bg-blue-500/20 text-blue-400 border border-blue-500/30' 
                : 'text-white/40 hover:text-white hover:bg-white/5 border border-transparent'
            }`}
          >
            {tab === 'saved' && `Saved Jobs (${savedJobs.length})`}
            {tab === 'discover' && 'Discover'}
            {tab === 'rss' && <><Rss className="inline h-4 w-4 mr-1" />RSS</>}
          </button>
        ))}
      </div>

      {/* Add Job Modal */}
      {showAddJob && (
        <div className="glass-card p-6">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-lg font-semibold text-white">Add Job Manually</h3>
            <Button variant="ghost" size="sm" onClick={() => setShowAddJob(false)} className="text-white/40 hover:text-white hover:bg-white/5">
              <X className="h-4 w-4" />
            </Button>
          </div>
          
          {jobError && (
            <div className="mb-4 text-sm text-red-400 bg-red-500/10 border border-red-500/20 p-3 rounded-lg">
              {jobError}
            </div>
          )}
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <input
              type="text"
              placeholder="Job Title *"
              value={newJob.title}
              onChange={(e) => setNewJob({ ...newJob, title: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            />
            <input
              type="text"
              placeholder="Company *"
              value={newJob.company}
              onChange={(e) => setNewJob({ ...newJob, company: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            />
            <input
              type="text"
              placeholder="Location"
              value={newJob.location}
              onChange={(e) => setNewJob({ ...newJob, location: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            />
            <select
              value={newJob.jobType}
              onChange={(e) => setNewJob({ ...newJob, jobType: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            >
              <option value="full_time">Full Time</option>
              <option value="part_time">Part Time</option>
              <option value="contract">Contract</option>
              <option value="internship">Internship</option>
              <option value="remote">Remote</option>
            </select>
            <input
              type="text"
              placeholder="Salary Range (e.g. $80k - $120k)"
              value={newJob.salaryRange}
              onChange={(e) => setNewJob({ ...newJob, salaryRange: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            />
            <input
              type="url"
              placeholder="Job URL"
              value={newJob.url}
              onChange={(e) => setNewJob({ ...newJob, url: e.target.value })}
              className="px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
            />
          </div>
          <textarea
            placeholder="Job Description"
            value={newJob.description}
            onChange={(e) => setNewJob({ ...newJob, description: e.target.value })}
            rows={4}
            className="w-full mt-4 px-4 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
          />
          <div className="flex gap-2 mt-4">
            <Button onClick={createJob} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">
              <Plus className="mr-2 h-4 w-4" /> Create Job
            </Button>
            <Button variant="outline" onClick={() => setShowAddJob(false)} className="border-white/10 hover:bg-white/5">Cancel</Button>
          </div>
        </div>
      )}

      {activeTab === 'saved' && (
        <div className="space-y-4">
          {savedJobs.length === 0 && !showAddJob && (
            <div className="text-center py-12 glass-card">
              <Briefcase className="h-12 w-12 text-white/10 mx-auto mb-3" />
              <p className="text-white/30">No saved jobs yet.</p>
              <p className="text-white/20 text-sm mt-1">Add a job manually or discover from external sources.</p>
            </div>
          )}

          {savedJobs.map((job) => (
            <div key={job.id} className="glass-card-hover p-6">
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <h3 className="font-semibold text-lg text-white">{job.title}</h3>
                    <span className={`text-xs px-2 py-0.5 rounded ${
                      job.status === 'saved' ? 'bg-white/5 text-white/40' :
                      job.status === 'applied' ? 'bg-blue-500/10 text-blue-400' :
                      job.status === 'interview' ? 'bg-amber-500/10 text-amber-400' :
                      job.status === 'offer' ? 'bg-green-500/10 text-green-400' :
                      'bg-white/5 text-white/40'
                    }`}>
                      {job.status}
                    </span>
                    {job.matchScore && (
                      <span className={`text-xs px-2 py-0.5 rounded ${
                        job.matchScore >= 80 ? 'bg-green-500/10 text-green-400' :
                        job.matchScore >= 60 ? 'bg-amber-500/10 text-amber-400' :
                        'bg-red-500/10 text-red-400'
                      }`}>
                        Match: {job.matchScore}%
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-white/40">{job.company} {job.location && `· ${job.location}`}</p>
                  {job.salaryRange && <p className="text-sm text-white/30">{job.salaryRange}</p>}
                  <p className="text-sm text-white/30 mt-2 line-clamp-2">{job.description}</p>
                </div>
                <div className="flex gap-2 ml-4">
                  {job.url && (
                    <a href={job.url} target="_blank" rel="noopener noreferrer" className="text-white/20 hover:text-blue-400 transition-colors">
                      <ExternalLink className="h-4 w-4" />
                    </a>
                  )}
                  <button onClick={() => analyzeMatch(job.id)} disabled={analyzingId === job.id} title="Match Analysis" className="text-white/20 hover:text-blue-400 transition-colors">
                    <Target className={`h-4 w-4 ${analyzingId === job.id ? 'animate-pulse' : ''}`} />
                  </button>
                  <button onClick={() => generateInterviewQa(job.id)} disabled={analyzingId === job.id} title="Interview Q&A" className="text-white/20 hover:text-blue-400 transition-colors">
                    <MessageSquare className={`h-4 w-4 ${analyzingId === job.id ? 'animate-pulse' : ''}`} />
                  </button>
                  <button onClick={() => analyzeJob(job.id)} disabled={analyzingId === job.id} title="AI Analysis" className="text-white/20 hover:text-blue-400 transition-colors">
                    <Sparkles className={`h-4 w-4 ${analyzingId === job.id ? 'animate-pulse' : ''}`} />
                  </button>
                  {job.status === 'saved' && (
                    <button onClick={() => applyToJob(job.id)} title="Mark Applied" className="text-white/20 hover:text-green-400 transition-colors">
                      <Plus className="h-4 w-4" />
                    </button>
                  )}
                  <button onClick={() => deleteJob(job.id)} title="Delete" className="text-white/20 hover:text-red-400 transition-colors">
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {activeTab === 'discover' && (
        <div className="space-y-6">
          <div className="glass-card p-6">
            <div className="flex gap-4">
              <div className="flex-1">
                <label className="text-sm font-medium text-white/60">Keywords</label>
                <div className="relative mt-1">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-white/20" />
                  <input
                    type="text"
                    value={searchKeywords}
                    onChange={(e) => setSearchKeywords(e.target.value)}
                    className="w-full pl-10 pr-3 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
                    placeholder="Software Engineer, React, etc."
                  />
                </div>
              </div>
              <div className="flex-1">
                <label className="text-sm font-medium text-white/60">Location</label>
                <input
                  type="text"
                  value={searchLocation}
                  onChange={(e) => setSearchLocation(e.target.value)}
                  className="w-full mt-1 px-3 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
                  placeholder="Remote, Los Angeles, etc."
                />
              </div>
              <div className="flex items-end">
                <Button onClick={discoverJobs} disabled={isDiscovering} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">
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
            <div className="text-center py-12 glass-card">
              <Search className="h-12 w-12 text-white/10 mx-auto mb-3" />
              <p className="text-white/30">Click Search to discover jobs from external sources.</p>
            </div>
          )}

          <div className="space-y-4">
            {discoveredJobs.map((job, idx) => (
              <div key={idx} className="glass-card-hover p-6">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <h3 className="font-semibold text-white">{job.title}</h3>
                    <p className="text-sm text-white/40">{job.company} {job.location && `· ${job.location}`}</p>
                    <span className="text-xs bg-white/5 px-2 py-0.5 rounded text-white/40">{job.source}</span>
                    <p className="text-sm text-white/30 mt-2 line-clamp-3">{job.description}</p>
                  </div>
                  <div className="flex gap-2 ml-4">
                    <Button size="sm" variant="outline" onClick={() => saveJob(job)} className="border-white/10 hover:bg-white/5">
                      <Save className="mr-2 h-4 w-4" />
                      Save
                    </Button>
                    {job.url && (
                      <a href={job.url} target="_blank" rel="noopener noreferrer" className="text-white/20 hover:text-blue-400 transition-colors mt-2">
                        <ExternalLink className="h-4 w-4" />
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
          <div className="glass-card p-6">
            <h3 className="font-semibold text-white mb-2">RSS Job Feed</h3>
            <div className="flex gap-2">
              <input
                type="text"
                placeholder="https://example.com/jobs.rss"
                className="flex-1 px-3 py-2.5 rounded-lg border border-white/10 bg-white/5 text-white text-sm placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-blue-500/40 focus:border-blue-500/30 transition-all"
                value={rssUrl}
                onChange={e => setRssUrl(e.target.value)}
              />
              <Button onClick={fetchRss} disabled={isFetchingRss} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">
                {isFetchingRss ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Rss className="mr-2 h-4 w-4" />}
                Fetch
              </Button>
            </div>
          </div>

          <div className="space-y-4">
            {rssJobs.map((job, idx) => (
              <div key={idx} className="glass-card-hover p-6">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <h3 className="font-semibold text-white">{job.title}</h3>
                    <p className="text-sm text-white/40">{job.company} {job.publishedAt && `· ${job.publishedAt}`}</p>
                    <p className="text-sm text-white/30 mt-2 line-clamp-3">{job.description}</p>
                  </div>
                  <div className="flex gap-2 ml-4">
                    <Button size="sm" variant="outline" onClick={() => importRssJob(job)} className="border-white/10 hover:bg-white/5">
                      <Save className="mr-2 h-4 w-4" />
                      Import
                    </Button>
                    {job.link && (
                      <a href={job.link} target="_blank" rel="noopener noreferrer" className="text-white/20 hover:text-blue-400 transition-colors mt-2">
                        <ExternalLink className="h-4 w-4" />
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
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50" onClick={() => setShowMatchModal(false)}>
          <div className="glass-card p-6 w-[600px] max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h2 className="text-xl font-semibold text-white mb-4">Match Analysis</h2>
            <div className="text-center mb-4">
              <p className="text-4xl font-bold text-blue-400">{matchResult.matchScore}%</p>
              <p className="text-sm text-white/40">Match Score</p>
            </div>
            <p className="text-sm text-white/60 mb-4">{matchResult.summary}</p>
            {matchResult.matchedSkills?.length > 0 && (
              <div className="mb-3">
                <h3 className="text-sm font-medium text-green-400">Matched Skills</h3>
                <p className="text-sm text-white/60">{matchResult.matchedSkills.join(', ')}</p>
              </div>
            )}
            {matchResult.missingSkills?.length > 0 && (
              <div className="mb-3">
                <h3 className="text-sm font-medium text-amber-400">Missing Skills</h3>
                <p className="text-sm text-white/60">{matchResult.missingSkills.join(', ')}</p>
              </div>
            )}
            {matchResult.suggestedImprovements?.length > 0 && (
              <div className="mb-3">
                <h3 className="text-sm font-medium text-white">Suggestions</h3>
                <ul className="text-sm text-white/60 list-disc pl-4">
                  {matchResult.suggestedImprovements.map((s: string, i: number) => <li key={i}>{s}</li>)}
                </ul>
              </div>
            )}
            <Button onClick={() => setShowMatchModal(false)} className="bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">Close</Button>
          </div>
        </div>
      )}

      {/* Interview Q&A Modal */}
      {showQaModal && interviewQa && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50" onClick={() => setShowQaModal(false)}>
          <div className="glass-card p-6 w-[700px] max-h-[80vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h2 className="text-xl font-semibold text-white mb-2">Interview Preparation</h2>
            <p className="text-sm text-white/40 mb-4">{interviewQa.roleFocus}</p>
            <p className="text-sm text-white/60 mb-4">{interviewQa.preparationTips}</p>
            <div className="space-y-4">
              {interviewQa.questions?.map((q: any, i: number) => (
                <div key={i} className="glass-card p-4">
                  <span className="text-xs bg-blue-500/10 text-blue-400 px-2 py-0.5 rounded mb-2 inline-block">{q.category}</span>
                  <p className="font-medium text-white mb-2">{q.question}</p>
                  {q.suggestedAnswer && (
                    <div className="bg-white/5 rounded-lg p-3 mt-2">
                      <p className="text-xs text-white/30 mb-1">Suggested Answer:</p>
                      <p className="text-sm text-white/60">{q.suggestedAnswer}</p>
                    </div>
                  )}
                  {q.keyPoints && <p className="text-xs text-white/30 mt-2">Key: {q.keyPoints}</p>}
                </div>
              ))}
            </div>
            <Button className="mt-4 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0" onClick={() => setShowQaModal(false)}>Close</Button>
          </div>
        </div>
      )}
    </div>
  )
}
