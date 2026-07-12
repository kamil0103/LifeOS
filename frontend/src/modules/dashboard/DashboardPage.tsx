import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Check, Flame, Briefcase, Code, Target, ChevronRight } from 'lucide-react'

interface TodayHabit {
  id: string
  name: string
  category: string
  icon?: string
  color?: string
  currentStreak: number
  isCompleted: boolean
}

interface JobStats {
  saved: number
  applied: number
  interview: number
  offer: number
}

interface DayProgress {
  date: string
  dayName: string
  completionCount: number
}

interface DashboardData {
  date: string
  habits: TodayHabit[]
  jobStats: JobStats
  totalXp: number
  level: number
  xpForNextLevel: number
  xpInCurrentLevel: number
  weeklyProgress: DayProgress[]
  totalStreakDays: number
}

export default function DashboardPage() {
  const navigate = useNavigate()
  const [data, setData] = useState<DashboardData | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [completingId, setCompletingId] = useState<string | null>(null)
  const [xpFlash, setXpFlash] = useState<number | null>(null)

  useEffect(() => {
    loadDashboard()
  }, [])

  const loadDashboard = () => {
    setIsLoading(true)
    api.get('/dashboard/today')
      .then(({ data }) => setData(data))
      .catch(console.error)
      .finally(() => setIsLoading(false))
  }

  const completeHabit = async (id: string) => {
    setCompletingId(id)
    try {
      const { data: result } = await api.post(`/habits/${id}/complete`)
      setXpFlash(result.xpEarned)
      setTimeout(() => setXpFlash(null), 2000)
      loadDashboard()
    } catch (err) {
      console.error(err)
    } finally {
      setCompletingId(null)
    }
  }

  const getProgressColor = (count: number) => {
    if (count === 0) return 'bg-muted'
    if (count <= 2) return 'bg-primary/30'
    if (count <= 4) return 'bg-primary/60'
    return 'bg-primary'
  }

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  if (!data) {
    return (
      <div className="p-8">
        <p className="text-muted-foreground">Failed to load dashboard data.</p>
      </div>
    )
  }

  const incompleteHabits = data.habits.filter(h => !h.isCompleted)
  const completedHabits = data.habits.filter(h => h.isCompleted)
  const progressPercent = data.xpForNextLevel > 0 ? (data.xpInCurrentLevel / data.xpForNextLevel) * 100 : 0

  return (
    <div className="p-8 max-w-6xl mx-auto">
      {/* XP Flash */}
      {xpFlash && (
        <div className="fixed top-20 right-8 bg-primary text-primary-foreground px-4 py-2 rounded-lg shadow-lg animate-bounce z-50">
          +{xpFlash} XP!
        </div>
      )}

      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">What should you do today?</h1>
        <p className="text-muted-foreground mt-1">
          {new Date(data.date).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })}
        </p>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {/* XP Card */}
        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <div className="flex items-center justify-between mb-2">
            <h2 className="text-sm font-medium text-muted-foreground">Level {data.level}</h2>
            <Target className="h-4 w-4 text-primary" />
          </div>
          <p className="text-2xl font-bold">{data.totalXp.toLocaleString()} XP</p>
          <div className="mt-3 h-2 bg-muted rounded-full overflow-hidden">
            <div
              className="h-full bg-primary transition-all"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
          <p className="text-xs text-muted-foreground mt-1">
            {data.xpInCurrentLevel} / {data.xpForNextLevel} XP to next level
          </p>
        </div>

        {/* Streak Card */}
        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <div className="flex items-center justify-between mb-2">
            <h2 className="text-sm font-medium text-muted-foreground">Best Streak</h2>
            <Flame className="h-4 w-4 text-orange-500" />
          </div>
          <p className="text-2xl font-bold">{data.totalStreakDays} days</p>
          <p className="text-xs text-muted-foreground mt-1">
            {completedHabits.length}/{data.habits.length} habits done today
          </p>
        </div>

        {/* Jobs Card */}
        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <div className="flex items-center justify-between mb-2">
            <h2 className="text-sm font-medium text-muted-foreground">Applications</h2>
            <Briefcase className="h-4 w-4 text-primary" />
          </div>
          <p className="text-2xl font-bold">{data.jobStats.applied}</p>
          <p className="text-xs text-muted-foreground mt-1">
            {data.jobStats.interview} interviews · {data.jobStats.offer} offers
          </p>
        </div>

        {/* Weekly Progress */}
        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <div className="flex items-center justify-between mb-2">
            <h2 className="text-sm font-medium text-muted-foreground">Last 7 Days</h2>
            <Code className="h-4 w-4 text-primary" />
          </div>
          <div className="flex gap-2 items-end h-12">
            {data.weeklyProgress.map((day) => (
              <div key={day.date} className="flex-1 flex flex-col items-center gap-1">
                <div
                  className={`w-full rounded-t-sm transition-all ${getProgressColor(day.completionCount)}`}
                  style={{ height: `${Math.min(day.completionCount * 20 + 4, 48)}px` }}
                />
                <span className="text-xs text-muted-foreground">{day.dayName}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Today's Habits */}
      <div className="mb-8">
        <h2 className="text-xl font-semibold mb-4">Today's Habits</h2>
        <div className="space-y-3">
          {incompleteHabits.length === 0 && completedHabits.length === 0 && (
            <div className="text-center py-8 text-muted-foreground bg-card border rounded-lg">
              No habits set up yet. Go to the Habits page to create some.
            </div>
          )}

          {incompleteHabits.map((habit) => (
            <div key={habit.id} className="bg-card border rounded-lg p-4 shadow-sm flex items-center justify-between">
              <div className="flex items-center gap-4">
                <Button
                  size="sm"
                  onClick={() => completeHabit(habit.id)}
                  disabled={completingId === habit.id}
                >
                  {completingId === habit.id ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Check className="h-4 w-4" />
                  )}
                </Button>
                <div>
                  <h3 className="font-medium">{habit.name}</h3>
                  <p className="text-sm text-muted-foreground">
                    {habit.category}
                    {habit.currentStreak > 0 && (
                      <span className="ml-2 text-orange-500">
                        🔥 {habit.currentStreak} day streak
                      </span>
                    )}
                  </p>
                </div>
              </div>
            </div>
          ))}

          {completedHabits.length > 0 && (
            <div className="mt-4">
              <h3 className="text-sm font-medium text-muted-foreground mb-2">Completed</h3>
              {completedHabits.map((habit) => (
                <div key={habit.id} className="bg-card border rounded-lg p-4 opacity-60 flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <div className="h-8 w-8 rounded-full bg-green-500/10 flex items-center justify-center">
                      <Check className="h-4 w-4 text-green-500" />
                    </div>
                    <div>
                      <h3 className="font-medium line-through">{habit.name}</h3>
                      <p className="text-sm text-muted-foreground">
                        {habit.category}
                        {habit.currentStreak > 0 && (
                          <span className="ml-2 text-orange-500">
                            🔥 {habit.currentStreak} day streak
                          </span>
                        )}
                      </p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Quick Actions */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <button
          onClick={() => navigate('/jobs/discover')}
          className="bg-card border rounded-lg p-4 shadow-sm text-left hover:shadow-md transition-shadow"
        >
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-medium">Find Jobs</h3>
              <p className="text-sm text-muted-foreground">Discover new opportunities</p>
            </div>
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          </div>
        </button>

        <button
          onClick={() => navigate('/habits')}
          className="bg-card border rounded-lg p-4 shadow-sm text-left hover:shadow-md transition-shadow"
        >
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-medium">Manage Habits</h3>
              <p className="text-sm text-muted-foreground">Add or edit your habits</p>
            </div>
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          </div>
        </button>

        <button
          onClick={() => navigate('/skills')}
          className="bg-card border rounded-lg p-4 shadow-sm text-left hover:shadow-md transition-shadow"
        >
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-medium">Update Skills</h3>
              <p className="text-sm text-muted-foreground">Track your progress</p>
            </div>
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          </div>
        </button>
      </div>
    </div>
  )
}
