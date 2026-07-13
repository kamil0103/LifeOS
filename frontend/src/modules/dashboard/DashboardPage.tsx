import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Check, Flame, Briefcase, Code, Target, ChevronRight, Sparkles, AlertTriangle, Lightbulb, Code2, BookOpen, Zap, Trophy, Star, BrainCircuit } from 'lucide-react'

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

interface MissionPriority {
  title: string
  category: string
  priority: string
  reason: string
}

interface DailyMission {
  id: string
  date: string
  priorities: MissionPriority[]
  aiSummary?: string
  isCompleted: boolean
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

interface AiMessage {
  id: string
  messageType: string
  content: string
}

interface CodingStats {
  solvedProblems: number
  currentStreak: number
  totalXpEarned: number
}

interface DailyVerseData {
  id: string
  reference: string
  text: string
}

export default function DashboardPage() {
  const navigate = useNavigate()
  const [data, setData] = useState<DashboardData | null>(null)
  const [mission, setMission] = useState<DailyMission | null>(null)
  const [aiMessages, setAiMessages] = useState<AiMessage[]>([])
  const [codingStats, setCodingStats] = useState<CodingStats | null>(null)
  const [dailyVerse, setDailyVerse] = useState<DailyVerseData | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [completingId, setCompletingId] = useState<string | null>(null)
  const [xpFlash, setXpFlash] = useState<number | null>(null)
  const [isGeneratingMission, setIsGeneratingMission] = useState(false)

  useEffect(() => {
    loadAllData()
  }, [])

  const loadAllData = async () => {
    setIsLoading(true)
    try {
      const { data: dashData } = await api.get('/dashboard/today')
      setData(dashData)
    } catch (err) {
      console.error(err)
    }

    try {
      const { data: missionData } = await api.get('/aicoach/mission')
      setMission(missionData)
    } catch (err) {
      // No mission yet
    }

    try {
      const { data: messages } = await api.get('/aicoach/messages?limit=2')
      setAiMessages(messages)
    } catch (err) {
      console.error(err)
    }

    try {
      const { data: cstats } = await api.get('/coding/stats')
      setCodingStats(cstats)
    } catch (err) {
      // Coding module may not have data yet
    }

    try {
      const { data: verse } = await api.get('/bible/daily')
      setDailyVerse(verse)
    } catch (err) {
      // Bible may not be seeded yet
    }

    setIsLoading(false)
  }

  const generateMission = async () => {
    setIsGeneratingMission(true)
    try {
      const { data } = await api.post('/aicoach/generate-mission')
      setMission(data)
      const { data: messages } = await api.get('/aicoach/messages?limit=2')
      setAiMessages(messages)
    } catch (err: any) {
      if (err.response?.data?.detail) {
        alert(err.response.data.detail)
      }
    } finally {
      setIsGeneratingMission(false)
    }
  }

  const completeHabit = async (id: string) => {
    setCompletingId(id)
    try {
      const { data: result } = await api.post(`/habits/${id}/complete`)
      setXpFlash(result.xpEarned)
      setTimeout(() => setXpFlash(null), 2000)
      loadAllData()
    } catch (err) {
      console.error(err)
    } finally {
      setCompletingId(null)
    }
  }

  const getProgressColor = (count: number) => {
    if (count === 0) return 'bg-white/5'
    if (count <= 2) return 'bg-blue-500/40'
    if (count <= 4) return 'bg-blue-500/70'
    return 'bg-blue-500'
  }

  const getPriorityColor = (priority: string) => {
    switch (priority.toLowerCase()) {
      case 'high': return 'bg-red-500/10 text-red-400 border-red-500/20'
      case 'medium': return 'bg-amber-500/10 text-amber-400 border-amber-500/20'
      case 'low': return 'bg-blue-500/10 text-blue-400 border-blue-500/20'
      default: return 'bg-white/5 text-white/60 border-white/5'
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="flex flex-col items-center gap-4">
          <div className="h-10 w-10 rounded-full border-2 border-blue-500/30 border-t-blue-500 animate-spin" />
          <p className="text-sm text-white/40">Loading your dashboard...</p>
        </div>
      </div>
    )
  }

  if (!data) {
    return (
      <div className="p-8">
        <p className="text-white/40">Failed to load dashboard data.</p>
      </div>
    )
  }

  const incompleteHabits = data.habits.filter(h => !h.isCompleted)
  const completedHabits = data.habits.filter(h => h.isCompleted)
  const progressPercent = data.xpForNextLevel > 0 ? (data.xpInCurrentLevel / data.xpForNextLevel) * 100 : 0

  return (
    <div className="max-w-7xl mx-auto space-y-6">
      {/* XP Flash */}
      {xpFlash && (
        <div className="fixed top-20 right-8 glass-card px-5 py-3 z-50 animate-fade-in-up">
          <div className="flex items-center gap-2">
            <Zap className="h-5 w-5 text-amber-400" />
            <span className="text-lg font-bold text-amber-400">+{xpFlash} XP!</span>
          </div>
        </div>
      )}

      {/* Header */}
      <div className="animate-fade-in-up">
        <h1 className="page-header">What should you do today?</h1>
        <p className="text-white/40 mt-1 text-sm">
          {new Date(data.date).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' })}
        </p>
      </div>

      {/* AI Mission + Daily Verse Row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* AI Mission */}
        <div className="lg:col-span-2 animate-fade-in-up animate-delay-1">
          {!mission ? (
            <div className="glass-card-hover p-6 border-dashed border-blue-500/20">
              <div className="flex items-center justify-between">
                <div className="flex items-start gap-4">
                  <div className="h-12 w-12 rounded-xl bg-gradient-to-br from-blue-500/20 to-purple-500/20 border border-blue-500/20 flex items-center justify-center shrink-0">
                    <Sparkles className="h-6 w-6 text-blue-400" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold text-white">Generate Your Daily Mission</h2>
                    <p className="text-sm text-white/40 mt-1">
                      Let AI analyze your progress and create personalized priorities
                    </p>
                  </div>
                </div>
                <Button onClick={generateMission} disabled={isGeneratingMission} className="shrink-0 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 border-0">
                  {isGeneratingMission ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Sparkles className="mr-2 h-4 w-4" />
                  )}
                  Generate
                </Button>
              </div>
            </div>
          ) : (
            <div className={`glass-card p-6 ${mission.isCompleted ? 'opacity-60' : 'border-blue-500/10'}`}>
              <div className="flex items-center justify-between mb-5">
                <div className="flex items-center gap-3">
                  <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-blue-500/20 to-purple-500/20 border border-blue-500/20 flex items-center justify-center">
                    <Target className="h-5 w-5 text-blue-400" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold text-white">
                      {mission.isCompleted ? <span className="line-through">Today's Mission</span> : "Today's Mission"}
                    </h2>
                    {mission.aiSummary && (
                      <p className="text-sm text-white/40 italic">"{mission.aiSummary}"</p>
                    )}
                  </div>
                </div>
                {!mission.isCompleted ? (
                  <Button size="sm" variant="outline" onClick={() => navigate('/ai-coach')} className="border-white/10 hover:bg-white/5">
                    View Details
                  </Button>
                ) : (
                  <span className="text-sm text-emerald-400 flex items-center gap-1">
                    <Check className="h-4 w-4" />
                    Completed
                  </span>
                )}
              </div>
              <div className="space-y-2">
                {mission.priorities.slice(0, 3).map((priority, idx) => (
                  <div
                    key={idx}
                    className={`border rounded-lg p-3 text-sm ${getPriorityColor(priority.priority)}`}
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-medium">{priority.title}</span>
                      <span className="text-xs opacity-70">{priority.category}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Daily Verse */}
        {dailyVerse && (
          <div className="animate-fade-in-up animate-delay-2">
            <div className="glass-card p-6 h-full border-amber-500/10">
              <div className="flex items-center gap-2 mb-3">
                <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-amber-500/20 to-orange-500/20 border border-amber-500/20 flex items-center justify-center">
                  <BookOpen className="h-4 w-4 text-amber-400" />
                </div>
                <span className="text-xs font-medium text-amber-400/80 uppercase tracking-wider">Daily Verse</span>
              </div>
              <p className="text-base italic leading-relaxed text-white/80">"{dailyVerse.text}"</p>
              <p className="text-sm text-white/40 mt-3">— {dailyVerse.reference}</p>
              <Button size="sm" variant="ghost" onClick={() => navigate('/bible')} className="mt-3 text-xs text-white/40 hover:text-white hover:bg-white/5">
                Open Bible →
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* AI Messages */}
      {aiMessages.length > 0 && (
        <div className="space-y-3 animate-fade-in-up animate-delay-2">
          {aiMessages.map((msg) => (
            <div
              key={msg.id}
              className={`glass-card p-4 ${msg.messageType === 'warning' ? 'border-amber-500/20' : 'border-blue-500/10'}`}
            >
              <div className="flex items-start gap-3">
                <div className={`h-8 w-8 rounded-lg flex items-center justify-center shrink-0 ${msg.messageType === 'warning' ? 'bg-amber-500/10' : 'bg-blue-500/10'}`}>
                  {msg.messageType === 'warning' ? (
                    <AlertTriangle className="h-4 w-4 text-amber-400" />
                  ) : (
                    <Lightbulb className="h-4 w-4 text-blue-400" />
                  )}
                </div>
                <p className="text-sm text-white/70 leading-relaxed">{msg.content}</p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Stats Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4 animate-fade-in-up animate-delay-3">
        {/* XP Card */}
        <div className="glass-card-hover p-5 stat-glow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-medium text-white/40 uppercase tracking-wider">Level {data.level}</span>
            <div className="h-8 w-8 rounded-lg bg-blue-500/10 flex items-center justify-center">
              <Trophy className="h-4 w-4 text-blue-400" />
            </div>
          </div>
          <p className="text-2xl font-bold text-white">{data.totalXp.toLocaleString()}</p>
          <p className="text-xs text-white/40 mt-1">Total XP</p>
          <div className="mt-3 h-1.5 bg-white/5 rounded-full overflow-hidden">
            <div className="h-full bg-gradient-to-r from-blue-500 to-blue-400 transition-all rounded-full" style={{ width: `${progressPercent}%` }} />
          </div>
          <p className="text-[10px] text-white/30 mt-1.5">
            {data.xpInCurrentLevel} / {data.xpForNextLevel} to next level
          </p>
        </div>

        {/* Streak Card */}
        <div className="glass-card-hover p-5 stat-glow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-medium text-white/40 uppercase tracking-wider">Best Streak</span>
            <div className="h-8 w-8 rounded-lg bg-orange-500/10 flex items-center justify-center">
              <Flame className="h-4 w-4 text-orange-400" />
            </div>
          </div>
          <p className="text-2xl font-bold text-white">{data.totalStreakDays}</p>
          <p className="text-xs text-white/40 mt-1">Days</p>
          <p className="text-[10px] text-white/30 mt-3">
            {completedHabits.length}/{data.habits.length} habits today
          </p>
        </div>

        {/* Jobs Card */}
        <div className="glass-card-hover p-5 stat-glow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-medium text-white/40 uppercase tracking-wider">Applications</span>
            <div className="h-8 w-8 rounded-lg bg-emerald-500/10 flex items-center justify-center">
              <Briefcase className="h-4 w-4 text-emerald-400" />
            </div>
          </div>
          <p className="text-2xl font-bold text-white">{data.jobStats.applied}</p>
          <p className="text-xs text-white/40 mt-1">Applied</p>
          <p className="text-[10px] text-white/30 mt-3">
            {data.jobStats.interview} interviews · {data.jobStats.offer} offers
          </p>
        </div>

        {/* Weekly Progress */}
        <div className="glass-card-hover p-5 stat-glow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-medium text-white/40 uppercase tracking-wider">Last 7 Days</span>
            <div className="h-8 w-8 rounded-lg bg-purple-500/10 flex items-center justify-center">
              <Code className="h-4 w-4 text-purple-400" />
            </div>
          </div>
          <div className="flex gap-1.5 items-end h-10">
            {data.weeklyProgress.map((day) => (
              <div key={day.date} className="flex-1 flex flex-col items-center gap-1.5">
                <div className={`w-full rounded-t-sm transition-all ${getProgressColor(day.completionCount)}`} style={{ height: `${Math.min(day.completionCount * 10 + 4, 40)}px` }} />
                <span className="text-[10px] text-white/30">{day.dayName}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Coding Card */}
        <div className="glass-card-hover p-5 stat-glow cursor-pointer" onClick={() => navigate('/coding')}>
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-medium text-white/40 uppercase tracking-wider">Coding</span>
            <div className="h-8 w-8 rounded-lg bg-cyan-500/10 flex items-center justify-center">
              <Code2 className="h-4 w-4 text-cyan-400" />
            </div>
          </div>
          <p className="text-2xl font-bold text-white">{codingStats?.solvedProblems ?? 0}</p>
          <p className="text-xs text-white/40 mt-1">Problems Solved</p>
          <p className="text-[10px] text-white/30 mt-3">
            {codingStats ? `${codingStats.currentStreak}d streak · ${codingStats.totalXpEarned} XP` : 'Start tracking'}
          </p>
        </div>
      </div>

      {/* Today's Habits */}
      <div className="animate-fade-in-up animate-delay-4">
        <div className="flex items-center gap-3 mb-4">
          <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-green-500/20 to-emerald-500/20 border border-green-500/20 flex items-center justify-center">
            <Star className="h-4 w-4 text-green-400" />
          </div>
          <h2 className="text-lg font-semibold text-white">Today's Habits</h2>
        </div>
        
        <div className="space-y-2">
          {incompleteHabits.length === 0 && completedHabits.length === 0 && (
            <div className="text-center py-12 glass-card">
              <p className="text-white/30">No habits set up yet.</p>
              <Button variant="ghost" onClick={() => navigate('/habits')} className="mt-2 text-blue-400 hover:text-blue-300 hover:bg-blue-500/5">
                Create your first habit →
              </Button>
            </div>
          )}

          {incompleteHabits.map((habit) => (
            <div key={habit.id} className="glass-card-hover p-4 flex items-center justify-between group">
              <div className="flex items-center gap-4">
                <Button
                  size="sm"
                  onClick={() => completeHabit(habit.id)}
                  disabled={completingId === habit.id}
                  className="h-9 w-9 rounded-full p-0 bg-white/5 hover:bg-green-500/20 border border-white/10 hover:border-green-500/30 transition-all"
                >
                  {completingId === habit.id ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Check className="h-4 w-4 text-green-400" />
                  )}
                </Button>
                <div>
                  <h3 className="font-medium text-white">{habit.name}</h3>
                  <p className="text-sm text-white/40">
                    {habit.category}
                    {habit.currentStreak > 0 && (
                      <span className="ml-2 text-orange-400">
                        <Flame className="h-3 w-3 inline mr-0.5" />
                        {habit.currentStreak} day streak
                      </span>
                    )}
                  </p>
                </div>
              </div>
            </div>
          ))}

          {completedHabits.length > 0 && (
            <div className="mt-4 space-y-2">
              <p className="text-xs font-medium text-white/30 uppercase tracking-wider ml-1">Completed</p>
              {completedHabits.map((habit) => (
                <div key={habit.id} className="glass-card p-4 opacity-50 flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <div className="h-9 w-9 rounded-full bg-green-500/10 flex items-center justify-center">
                      <Check className="h-4 w-4 text-green-400" />
                    </div>
                    <div>
                      <h3 className="font-medium text-white line-through">{habit.name}</h3>
                      <p className="text-sm text-white/30">
                        {habit.category}
                        {habit.currentStreak > 0 && (
                          <span className="ml-2 text-orange-400/60">
                            <Flame className="h-3 w-3 inline mr-0.5" />
                            {habit.currentStreak} day streak
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
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 animate-fade-in-up animate-delay-5">
        {[
          { path: '/jobs', title: 'Find Jobs', desc: 'Discover opportunities', icon: Briefcase, color: 'from-blue-500/20 to-blue-600/10 border-blue-500/20' },
          { path: '/habits', title: 'Manage Habits', desc: 'Build consistency', icon: Target, color: 'from-green-500/20 to-green-600/10 border-green-500/20' },
          { path: '/coding', title: 'Solve Problems', desc: 'Track practice', icon: Code2, color: 'from-cyan-500/20 to-cyan-600/10 border-cyan-500/20' },
          { path: '/ai-coach', title: 'AI Coach', desc: 'Get guidance', icon: BrainCircuit, color: 'from-purple-500/20 to-purple-600/10 border-purple-500/20' },
        ].map((action) => (
          <button
            key={action.path}
            onClick={() => navigate(action.path)}
            className={`glass-card-hover p-5 text-left group bg-gradient-to-br ${action.color}`}
          >
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-medium text-white group-hover:text-white transition-colors">{action.title}</h3>
                <p className="text-sm text-white/40 mt-1">{action.desc}</p>
              </div>
              <ChevronRight className="h-5 w-5 text-white/20 group-hover:text-white/40 group-hover:translate-x-1 transition-all" />
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}
