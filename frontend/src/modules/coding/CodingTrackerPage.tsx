import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Code2, Trophy, Flame, Clock, CheckCircle, Circle, RotateCcw, Plus, Trash2, Filter } from 'lucide-react'

interface Problem {
  id: string
  title: string
  platform?: string
  url?: string
  difficulty: string
  category?: string
  description?: string
  notes?: string
  isSolved: boolean
  solvedAt?: string
  solutionLanguage?: string
  timeSpentMinutes?: number
  attemptCount: number
}

interface Stats {
  totalProblems: number
  solvedProblems: number
  easySolved: number
  mediumSolved: number
  hardSolved: number
  currentStreak: number
  longestStreak: number
  totalXpEarned: number
  byCategory: { category: string; solvedCount: number }[]
  byLanguage: { language: string; solvedCount: number }[]
}

const DIFFICULTY_COLORS: Record<string, string> = {
  easy: 'bg-green-500/10 text-green-500 border-green-500/20',
  medium: 'bg-yellow-500/10 text-yellow-500 border-yellow-500/20',
  hard: 'bg-red-500/10 text-red-500 border-red-500/20',
}

export default function CodingTrackerPage() {
  const [problems, setProblems] = useState<Problem[]>([])
  const [stats, setStats] = useState<Stats | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [filterDifficulty, setFilterDifficulty] = useState<string>('')
  const [filterSolved, setFilterSolved] = useState<string>('')
  const [showAddForm, setShowAddForm] = useState(false)
  const [solvingId, setSolvingId] = useState<string | null>(null)

  const [formData, setFormData] = useState({
    title: '',
    platform: '',
    url: '',
    difficulty: 'easy',
    category: '',
    description: '',
    notes: ''
  })

  const [solveData, setSolveData] = useState<Record<string, { language: string; time: string; notes: string }>>({})

  useEffect(() => {
    loadData()
  }, [filterDifficulty, filterSolved])

  const loadData = async () => {
    setIsLoading(true)
    try {
      const params = new URLSearchParams()
      if (filterDifficulty) params.append('difficulty', filterDifficulty)
      if (filterSolved) params.append('solved', filterSolved)

      const [{ data: probs }, { data: s }] = await Promise.all([
        api.get(`/coding/problems?${params.toString()}`),
        api.get('/coding/stats')
      ])
      setProblems(probs)
      setStats(s)
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const createProblem = async () => {
    if (!formData.title.trim()) return
    try {
      await api.post('/coding/problems', formData)
      setShowAddForm(false)
      setFormData({ title: '', platform: '', url: '', difficulty: 'easy', category: '', description: '', notes: '' })
      loadData()
    } catch (err) {
      console.error(err)
    }
  }

  const solveProblem = async (id: string) => {
    setSolvingId(id)
    const data = solveData[id] || { language: '', time: '', notes: '' }
    try {
      await api.post(`/coding/problems/${id}/solve`, {
        solutionLanguage: data.language || undefined,
        timeSpentMinutes: data.time ? parseInt(data.time) : undefined,
        notes: data.notes || undefined
      })
      setSolveData(prev => { const n = { ...prev }; delete n[id]; return n })
      loadData()
    } catch (err) {
      console.error(err)
    } finally {
      setSolvingId(null)
    }
  }

  const unsolveProblem = async (id: string) => {
    try {
      await api.post(`/coding/problems/${id}/unsolve`)
      loadData()
    } catch (err) {
      console.error(err)
    }
  }

  const deleteProblem = async (id: string) => {
    if (!confirm('Delete this problem?')) return
    try {
      await api.delete(`/coding/problems/${id}`)
      loadData()
    } catch (err) {
      console.error(err)
    }
  }

  const getDifficultyLabel = (d: string) => d.charAt(0).toUpperCase() + d.slice(1)

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
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Code2 className="h-6 w-6 text-primary" />
          Coding Tracker
        </h1>
        <Button onClick={() => setShowAddForm(!showAddForm)}>
          <Plus className="mr-2 h-4 w-4" />
          Add Problem
        </Button>
      </div>

      {/* Stats Cards */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Solved</p>
            <p className="text-2xl font-bold">{stats.solvedProblems} <span className="text-sm font-normal text-muted-foreground">/ {stats.totalProblems}</span></p>
          </div>
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Current Streak</p>
            <p className="text-2xl font-bold flex items-center gap-1">
              <Flame className="h-5 w-5 text-orange-500" />
              {stats.currentStreak}d
            </p>
          </div>
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Best Streak</p>
            <p className="text-2xl font-bold">{stats.longestStreak}d</p>
          </div>
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Total XP</p>
            <p className="text-2xl font-bold flex items-center gap-1">
              <Trophy className="h-5 w-5 text-yellow-500" />
              {stats.totalXpEarned}
            </p>
          </div>
        </div>
      )}

      {/* Difficulty Breakdown */}
      {stats && (
        <div className="grid grid-cols-3 gap-3 mb-6">
          <div className="bg-green-500/5 border border-green-500/20 rounded-lg p-3 text-center">
            <p className="text-sm text-green-500 font-medium">Easy</p>
            <p className="text-xl font-bold text-green-500">{stats.easySolved}</p>
          </div>
          <div className="bg-yellow-500/5 border border-yellow-500/20 rounded-lg p-3 text-center">
            <p className="text-sm text-yellow-500 font-medium">Medium</p>
            <p className="text-xl font-bold text-yellow-500">{stats.mediumSolved}</p>
          </div>
          <div className="bg-red-500/5 border border-red-500/20 rounded-lg p-3 text-center">
            <p className="text-sm text-red-500 font-medium">Hard</p>
            <p className="text-xl font-bold text-red-500">{stats.hardSolved}</p>
          </div>
        </div>
      )}

      {/* Add Problem Form */}
      {showAddForm && (
        <div className="bg-card border rounded-lg p-6 mb-6 space-y-4">
          <h3 className="font-semibold">New Problem</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="text-sm font-medium mb-1 block">Title *</label>
              <input type="text" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.title} onChange={e => setFormData({ ...formData, title: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium mb-1 block">Platform</label>
              <input type="text" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.platform} onChange={e => setFormData({ ...formData, platform: e.target.value })} placeholder="LeetCode, HackerRank..." />
            </div>
            <div>
              <label className="text-sm font-medium mb-1 block">URL</label>
              <input type="text" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.url} onChange={e => setFormData({ ...formData, url: e.target.value })} />
            </div>
            <div>
              <label className="text-sm font-medium mb-1 block">Difficulty</label>
              <select className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.difficulty} onChange={e => setFormData({ ...formData, difficulty: e.target.value })}>
                <option value="easy">Easy</option>
                <option value="medium">Medium</option>
                <option value="hard">Hard</option>
              </select>
            </div>
            <div>
              <label className="text-sm font-medium mb-1 block">Category</label>
              <input type="text" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.category} onChange={e => setFormData({ ...formData, category: e.target.value })} placeholder="Arrays, DP, Graphs..." />
            </div>
            <div>
              <label className="text-sm font-medium mb-1 block">Description</label>
              <input type="text" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.description} onChange={e => setFormData({ ...formData, description: e.target.value })} />
            </div>
          </div>
          <div className="flex gap-2">
            <Button onClick={createProblem}>Save Problem</Button>
            <Button variant="outline" onClick={() => setShowAddForm(false)}>Cancel</Button>
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="flex gap-3 mb-4 items-center">
        <Filter className="h-4 w-4 text-muted-foreground" />
        <select className="px-3 py-1 border rounded-md bg-background text-sm" value={filterDifficulty} onChange={e => setFilterDifficulty(e.target.value)}>
          <option value="">All Difficulties</option>
          <option value="easy">Easy</option>
          <option value="medium">Medium</option>
          <option value="hard">Hard</option>
        </select>
        <select className="px-3 py-1 border rounded-md bg-background text-sm" value={filterSolved} onChange={e => setFilterSolved(e.target.value)}>
          <option value="">All Status</option>
          <option value="true">Solved</option>
          <option value="false">Unsolved</option>
        </select>
      </div>

      {/* Problem List */}
      <div className="space-y-3">
        {problems.length === 0 ? (
          <div className="bg-card border rounded-lg p-8 text-center">
            <Code2 className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-muted-foreground">No problems tracked yet.</p>
            <p className="text-sm text-muted-foreground mt-1">Add your first problem to start tracking.</p>
          </div>
        ) : (
          problems.map(problem => (
            <div key={problem.id} className={`bg-card border rounded-lg p-4 ${problem.isSolved ? 'opacity-80' : ''}`}>
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    {problem.isSolved ? (
                      <CheckCircle className="h-5 w-5 text-green-500" />
                    ) : (
                      <Circle className="h-5 w-5 text-muted-foreground" />
                    )}
                    <span className={`inline-block px-2 py-0.5 text-xs border rounded ${DIFFICULTY_COLORS[problem.difficulty] || 'bg-secondary'}`}>
                      {getDifficultyLabel(problem.difficulty)}
                    </span>
                    {problem.category && (
                      <span className="text-xs text-muted-foreground">{problem.category}</span>
                    )}
                    {problem.platform && (
                      <span className="text-xs text-muted-foreground">· {problem.platform}</span>
                    )}
                  </div>
                  <h3 className={`font-medium ${problem.isSolved ? 'line-through text-muted-foreground' : ''}`}>
                    {problem.url ? (
                      <a href={problem.url} target="_blank" rel="noopener noreferrer" className="hover:underline">{problem.title}</a>
                    ) : problem.title}
                  </h3>
                  {problem.description && (
                    <p className="text-sm text-muted-foreground mt-1">{problem.description}</p>
                  )}
                  {problem.isSolved && problem.solvedAt && (
                    <div className="flex items-center gap-3 mt-2 text-xs text-muted-foreground">
                      <span className="flex items-center gap-1"><CheckCircle className="h-3 w-3" /> Solved {new Date(problem.solvedAt).toLocaleDateString()}</span>
                      {problem.solutionLanguage && <span>{problem.solutionLanguage}</span>}
                      {problem.timeSpentMinutes && <span className="flex items-center gap-1"><Clock className="h-3 w-3" /> {problem.timeSpentMinutes}m</span>}
                      {problem.attemptCount > 1 && <span>{problem.attemptCount} attempts</span>}
                    </div>
                  )}
                </div>
                <div className="flex gap-1 ml-4">
                  {!problem.isSolved ? (
                    <div className="flex flex-col gap-2">
                      <div className="flex gap-1">
                        <input
                          type="text"
                          placeholder="Language"
                          className="w-24 px-2 py-1 text-xs border rounded bg-background"
                          value={solveData[problem.id]?.language || ''}
                          onChange={e => setSolveData(prev => ({ ...prev, [problem.id]: { ...prev[problem.id], language: e.target.value } }))}
                        />
                        <input
                          type="number"
                          placeholder="Min"
                          className="w-16 px-2 py-1 text-xs border rounded bg-background"
                          value={solveData[problem.id]?.time || ''}
                          onChange={e => setSolveData(prev => ({ ...prev, [problem.id]: { ...prev[problem.id], time: e.target.value } }))}
                        />
                      </div>
                      <Button size="sm" onClick={() => solveProblem(problem.id)} disabled={solvingId === problem.id}>
                        {solvingId === problem.id ? <Loader2 className="h-3 w-3 animate-spin" /> : 'Solve'}
                      </Button>
                    </div>
                  ) : (
                    <Button size="sm" variant="ghost" onClick={() => unsolveProblem(problem.id)} title="Mark unsolved">
                      <RotateCcw className="h-4 w-4" />
                    </Button>
                  )}
                  <Button size="sm" variant="ghost" className="text-destructive" onClick={() => deleteProblem(problem.id)}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
