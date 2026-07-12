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
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    loadData()
  }, [])

  const loadData = async () => {
    setIsLoading(true)
    try {
      const [{ data: xp }, { data: heat }, { data: funnel }, { data: streaks }, { data: over }] = await Promise.all([
        api.get('/analytics/xp-trends'),
        api.get('/analytics/habit-heatmap'),
        api.get('/analytics/job-funnel'),
        api.get('/analytics/coding-streaks'),
        api.get('/analytics/overview')
      ])
      setXpTrends(xp)
      setHabitHeatmap(heat)
      setJobFunnel(funnel)
      setCodingStreaks(streaks)
      setOverview(over)
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
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
      <h1 className="text-2xl font-bold mb-6 flex items-center gap-2">
        <TrendingUp className="h-6 w-6 text-primary" />
        Analytics
      </h1>

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
            <p className="text-sm text-muted-foreground">Coding</p>
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
            XP Trends (30 days)
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
                      className="h-full bg-primary rounded-full"
                      style={{ width: `${Math.min((day.xp / 50) * 100, 100)}%` }}
                    />
                  </div>
                  <span className="text-xs font-medium w-12 text-right">+{day.xp}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Habit Heatmap */}
        <div className="bg-card border rounded-lg p-6">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <CheckCircle className="h-5 w-5 text-green-500" />
            Habit Heatmap (90 days)
          </h2>
          {habitHeatmap.length === 0 ? (
            <p className="text-muted-foreground text-sm">No habit completions yet.</p>
          ) : (
            <div className="grid grid-cols-7 gap-1">
              {habitHeatmap.map(day => (
                <div
                  key={day.date}
                  className={`aspect-square rounded-sm ${
                    day.count === 0 ? 'bg-muted' :
                    day.count <= 2 ? 'bg-green-500/30' :
                    day.count <= 4 ? 'bg-green-500/60' :
                    'bg-green-500'
                  }`}
                  title={`${day.date}: ${day.count} habits`}
                />
              ))}
            </div>
          )}
        </div>

        {/* Job Funnel */}
        <div className="bg-card border rounded-lg p-6">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Briefcase className="h-5 w-5 text-primary" />
            Job Application Funnel
          </h2>
          {jobFunnel && (
            <div className="space-y-3">
              {[
                { label: 'Saved', value: jobFunnel.saved, color: 'bg-gray-500' },
                { label: 'Applied', value: jobFunnel.applied, color: 'bg-blue-500' },
                { label: 'Phone Screen', value: jobFunnel.phone_screen, color: 'bg-yellow-500' },
                { label: 'Interview', value: jobFunnel.interview, color: 'bg-purple-500' },
                { label: 'Offer', value: jobFunnel.offer, color: 'bg-green-500' },
                { label: 'Rejected', value: jobFunnel.rejected, color: 'bg-red-500' },
              ].map(stage => (
                <div key={stage.label} className="flex items-center gap-3">
                  <span className="text-xs w-24">{stage.label}</span>
                  <div className="flex-1 h-6 bg-muted rounded-full overflow-hidden">
                    <div
                      className={`h-full ${stage.color} rounded-full flex items-center justify-end px-2`}
                      style={{ width: `${jobFunnel.total > 0 ? (stage.value / jobFunnel.total) * 100 : 0}%` }}
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
                <p className="text-sm text-muted-foreground">Total Solves</p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
