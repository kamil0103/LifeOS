import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Loader2, TrendingUp, CheckCircle, Briefcase, Code2, Target } from 'lucide-react'

interface XpTrend {
  date: string
  xp: number
  source: Array<{ source: string; count: number; xp: number }>
}

interface HabitDay {
  date: string
  count: number
  habits: string[]
}

interface JobFunnel {
  withdrawn: number
  saved: number
  applied: number
  phone_screen: number
  interview: number
  offer: number
  rejected: number
  total: number
}

interface CodingStreak {
  current: number
  longest: number
  totalDays: number
  totalAttempts: number
  totalProblems: number
}

interface Overview {
  totalXp: number
  habitsCount: number
  jobsCount: number
  codingCount: number
  docsCount: number
}

export default function AnalyticsPage() {
  const [xpTrends, setXpTrends] = useState<XpTrend[]>([])
  const [habitHeatmap, setHabitHeatmap] = useState<HabitDay[]>([])
  const [jobFunnel, setJobFunnel] = useState<JobFunnel | null>(null)
  const [codingStreaks, setCodingStreaks] = useState<CodingStreak | null>(null)
  const [overview, setOverview] = useState<Overview | null>(null)
  const [period, setPeriod] = useState(30)
  const [range, setRange] = useState({ startDate: '', endDate: '', timeZone: '' })
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => { const controller = new AbortController(); void loadData(controller.signal); return () => controller.abort() }, [period])
  const loadData = async (signal?: AbortSignal) => {
    setIsLoading(true); setError('')
    try {
      const { data } = await api.get(`/analytics/report?days=${period}`, { signal })
      if (signal?.aborted) return
      setXpTrends(data.xpTrends); setHabitHeatmap(data.habitHeatmap); setJobFunnel(data.jobFunnel); setCodingStreaks(data.codingStreaks); setOverview(data.overview); setRange(data)
    } catch { if (!signal?.aborted) setError('Could not load analytics. Try again.') }
    finally { if (!signal?.aborted) setIsLoading(false) }
  }
  const maxXp = Math.max(1, ...xpTrends.map(d => Math.abs(d.xp)))
  const padding = habitHeatmap.length ? new Date(habitHeatmap[0].date + 'T12:00:00Z').getUTCDay() : 0

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  if (error) return <div className="p-8 max-w-6xl mx-auto"><h1 className="text-2xl font-bold mb-4">Analytics</h1><div role="alert">{error} <button className="underline" onClick={() => loadData()}>Retry</button></div></div>

  return (
    <div className="p-8 max-w-6xl mx-auto">
      <h1 className="text-2xl font-bold mb-6 flex items-center gap-2">
        <TrendingUp className="h-6 w-6 text-primary" />
        Analytics
      </h1>

      {error && <div role="alert" className="border border-destructive rounded p-3 mb-4">{error} <button className="underline" onClick={() => loadData()}>Retry</button></div>}
      <div className="flex flex-wrap items-center gap-3 mb-6">
        <label>Activity period <select aria-label="Activity period" className="ml-2 border rounded bg-background p-2" value={period} onChange={e => setPeriod(Number(e.target.value))}>{[7, 30, 90].map(days => <option key={days} value={days}>{days} days</option>)}</select></label>
        <span className="text-sm text-muted-foreground">{range.startDate} – {range.endDate} · {range.timeZone}</span>
      </div>
      <p className="text-sm text-muted-foreground mb-3">Account totals below are all time. XP and habit activity use the selected period.</p>
      {/* Overview Cards */}
      {overview && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-8">
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Total XP</p>
            <p className="text-2xl font-bold">{overview.totalXp.toLocaleString()}</p>
          </div>
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Habits</p>
            <p className="text-2xl font-bold">{overview.habitsCount}</p>
          </div>
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Jobs</p>
            <p className="text-2xl font-bold">{overview.jobsCount}</p>
          </div>
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Unique coding problems</p>
            <p className="text-2xl font-bold">{overview.codingCount}</p>
          </div>
          <div className="bg-card border rounded-lg p-4">
            <p className="text-sm text-muted-foreground">Documents</p>
            <p className="text-2xl font-bold">{overview.docsCount}</p>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* XP Trends */}
        <div className="bg-card border rounded-lg p-6">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Target className="h-5 w-5 text-primary" />
            XP Trends ({period} days)
          </h2>
          {xpTrends.length === 0 ? (
            <p className="text-muted-foreground text-sm">No XP data yet.</p>
          ) : (
            <div className="space-y-2">
              {xpTrends.map(day => (
                <div key={day.date} className="flex items-center gap-3">
                  <span className="text-xs text-muted-foreground w-20">{day.date}</span>
                  <div className="flex-1 h-4 bg-muted rounded-full overflow-hidden">
                    <div
                      title={day.source.map(s => `${s.source}: ${s.xp} XP`).join(', ')} className={`h-full rounded-full ${day.xp < 0 ? 'bg-red-500' : 'bg-primary'}`}
                      style={{ width: `${(Math.abs(day.xp) / maxXp) * 100}%` }}
                    />
                  </div>
                  <span className="text-xs font-medium w-12 text-right">{day.xp > 0 ? '+' : ''}{day.xp}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Habit Heatmap */}
        <div className="bg-card border rounded-lg p-6">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <CheckCircle className="h-5 w-5 text-green-500" />
            Habit Heatmap ({period} days)
          </h2>
          {habitHeatmap.length === 0 ? (
            <p className="text-muted-foreground text-sm">No habit completions yet.</p>
          ) : (
            <div className="grid grid-cols-7 gap-1">
              {['Sun','Mon','Tue','Wed','Thu','Fri','Sat'].map(d => <span key={d} className="text-[10px] text-muted-foreground text-center">{d}</span>)}
              {Array.from({ length: padding }, (_, i) => <div key={'pad-' + i} aria-hidden="true" />)}
              {habitHeatmap.map(day => (
                <div
                  key={day.date}
                  className={`aspect-square rounded-sm ${
                    day.count === 0 ? 'bg-muted' :
                    day.count <= 2 ? 'bg-green-500/30' :
                    day.count <= 4 ? 'bg-green-500/60' :
                    'bg-green-500'
                  }`}
                  role="img" aria-label={`${day.date}: ${day.count} habits`} title={`${day.date}: ${day.count} habits${day.habits.length ? ' · ' + day.habits.join(', ') : ''}`}
                />
              ))}
            </div>
          )}
        </div>

        {/* Job Funnel */}
        <div className="bg-card border rounded-lg p-6">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Briefcase className="h-5 w-5 text-primary" />
            Current Application Status
          </h2>
          {jobFunnel && (
            <div className="space-y-3">
              {[
                { label: 'Untracked jobs', value: jobFunnel.saved, color: 'bg-gray-500' },
                { label: 'Applied', value: jobFunnel.applied, color: 'bg-blue-500' },
                { label: 'Phone Screen', value: jobFunnel.phone_screen, color: 'bg-yellow-500' },
                { label: 'Interview', value: jobFunnel.interview, color: 'bg-purple-500' },
                { label: 'Offer', value: jobFunnel.offer, color: 'bg-green-500' },
                { label: 'Rejected', value: jobFunnel.rejected, color: 'bg-red-500' },
                { label: 'Withdrawn', value: jobFunnel.withdrawn, color: 'bg-slate-500' },
              ].map(stage => (
                <div key={stage.label} className="flex items-center gap-3">
                  <span className="text-xs w-24">{stage.label}</span>
                  <div className="flex-1 h-6 bg-muted rounded-full overflow-hidden">
                    <div
                      className={`h-full ${stage.color} rounded-full flex items-center justify-end px-2`}
                      style={{ width: `${jobFunnel.total + jobFunnel.saved > 0 ? (stage.value / (jobFunnel.total + jobFunnel.saved)) * 100 : 0}%` }}
                    >
                      {stage.value > 0 && <span className="text-xs text-white font-medium">{stage.value}</span>}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Coding Streaks */}
        <div className="bg-card border rounded-lg p-6">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Code2 className="h-5 w-5 text-primary" />
            Coding Streaks
          </h2>
          <p className="text-xs text-muted-foreground mb-3">All-time streaks and practice history; reviews count as attempts.</p>
          {codingStreaks && (
            <div className="grid grid-cols-2 gap-4">
              <div className="text-center p-4 bg-orange-500/5 rounded-lg">
                <p className="text-3xl font-bold text-orange-500">{codingStreaks.current}</p>
                <p className="text-sm text-muted-foreground">Current Streak</p>
              </div>
              <div className="text-center p-4 bg-primary/5 rounded-lg">
                <p className="text-3xl font-bold text-primary">{codingStreaks.longest}</p>
                <p className="text-sm text-muted-foreground">Best Streak</p>
              </div>
              <div className="text-center p-4 bg-secondary/50 rounded-lg">
                <p className="text-3xl font-bold">{codingStreaks.totalDays}</p>
                <p className="text-sm text-muted-foreground">Active Days</p>
              </div>
              <div className="text-center p-4 bg-secondary/50 rounded-lg">
                <p className="text-3xl font-bold">{codingStreaks.totalProblems}</p>
                <p className="text-xs text-muted-foreground">{codingStreaks.totalAttempts} total attempts</p>
                <p className="text-sm text-muted-foreground">Unique problems practiced</p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
