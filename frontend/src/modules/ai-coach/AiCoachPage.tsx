import CoachConversation from './CoachConversation'
import { notify } from '@/lib/notify'
import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Sparkles, Check, AlertTriangle, Lightbulb } from 'lucide-react'

interface MissionPriority {
  completed?: boolean
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

interface AiMessage {
  id: string
  messageType: string
  content: string
  isRead: boolean
  createdAt: string
}

export default function AiCoachPage() {
  const [mission, setMission] = useState<DailyMission | null>(null)
  const [messages, setMessages] = useState<AiMessage[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isGenerating, setIsGenerating] = useState(false)

  useEffect(() => {
    loadData()
  }, [])

  const loadData = async () => {
    setIsLoading(true)
    try {
      const { data: missionData } = await api.get('/aicoach/mission')
      setMission(missionData)
    } catch (err: any) {
      if (err.response?.status === 404) {
        setMission(null)
      }
    }

    try {
      const { data: messagesData } = await api.get('/aicoach/messages')
      setMessages(messagesData)
    } catch (err) {
      console.error(err)
    }

    setIsLoading(false)
  }

  const generateMission = async () => {
    setIsGenerating(true)
    try {
      const { data } = await api.post('/aicoach/generate-mission')
      setMission(data)
      // Refresh messages too
      const { data: messagesData } = await api.get('/aicoach/messages')
      setMessages(messagesData)
    } catch (err: any) {
      if (err.response?.data?.detail) {
        notify(err.response.data.detail)
      }
    } finally {
      setIsGenerating(false)
    }
  }

  const completeMission = async () => {
    if (!mission) return
    try {
      await api.post(`/aicoach/mission/${mission.id}/complete`)
      setMission({ ...mission, isCompleted: true })
    } catch (err) {
      console.error(err)
    }
  }

  const getPriorityColor = (priority: string) => {
    switch (priority.toLowerCase()) {
      case 'high': return 'bg-destructive/10 text-destructive border-destructive/20'
      case 'medium': return 'bg-yellow-500/10 text-yellow-600 border-yellow-500/20'
      case 'low': return 'bg-blue-500/10 text-blue-600 border-blue-500/20'
      default: return 'bg-secondary text-secondary-foreground'
    }
  }

  const getCategoryIcon = (category: string) => {
    switch (category.toLowerCase()) {
      case 'coding': return '💻'
      case 'jobs': return '💼'
      case 'habits': return '✅'
      case 'spiritual': return '🙏'
      default: return '⭐'
    }
  }

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-4xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">AI Coach</h1>
        <p className="text-muted-foreground mt-1">Your personal mentor for growth</p>
      </div>

      {/* Today's Mission */}
      <div className="mb-8">
        {!mission ? (
          <div className="bg-card border rounded-lg p-8 shadow-sm text-center">
            <Sparkles className="h-12 w-12 text-primary mx-auto mb-4" />
            <h2 className="text-xl font-semibold mb-2">Generate Today's Mission</h2>
            <p className="text-muted-foreground mb-6">
              Let the AI analyze your progress and create a personalized mission for today.
            </p>
            <Button onClick={generateMission} disabled={isGenerating} size="lg">
              {isGenerating ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Analyzing...
                </>
              ) : (
                <>
                  <Sparkles className="mr-2 h-4 w-4" />
                  Generate Mission
                </>
              )}
            </Button>
          </div>
        ) : (
          <div className={`bg-card border rounded-lg p-6 shadow-sm ${mission.isCompleted ? 'opacity-60' : ''}`}>
            <div className="flex items-center justify-between mb-4">
              <div>
                <h2 className="text-xl font-semibold">
                  {mission.isCompleted ? (
                    <span className="line-through">Today's Mission</span>
                  ) : (
                    "Today's Mission"
                  )}
                </h2>
                <p className="text-sm text-muted-foreground">
                  {new Date(mission.date).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })}
                </p>
              </div>
              {!mission.isCompleted && (
                <Button onClick={completeMission} variant="outline">
                  <Check className="mr-2 h-4 w-4" />
                  Mark Complete
                </Button>
              )}
              {mission.isCompleted && (
                <span className="text-sm text-green-500 flex items-center gap-1">
                  <Check className="h-4 w-4" />
                  Completed
                </span>
              )}
            </div>

            {mission.aiSummary && (
              <p className="text-muted-foreground mb-6 italic">"{mission.aiSummary}"</p>
            )}

            <div className="space-y-3">
              {mission.priorities.map((priority, idx) => (
                <div
                  key={idx}
                  className={`border rounded-lg p-4 ${getPriorityColor(priority.priority)}`}
                >
                  <div className="flex items-start gap-3">
                    <span className="text-2xl">{getCategoryIcon(priority.category)}</span>
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <h3 className={`font-medium ${priority.completed ? 'line-through opacity-60' : ''}`}>{priority.title}</h3>
                        <span className="text-xs px-2 py-0.5 rounded bg-background/50 uppercase">
                          {priority.priority}
                        </span>
                      </div>
                      <p className="text-sm mt-1 opacity-80">{priority.reason}</p>
                      {priority.completed ? <span className="text-xs">Completed</span> : !mission.isCompleted && <Button size="sm" variant="outline" className="mt-2" onClick={async () => { try { await api.post('/planning/complete', { actionId: `mission:${mission.id}:${idx}` }); const { data } = await api.get('/aicoach/mission'); setMission(data) } catch (err: any) { notify(err.response?.data?.detail || 'Could not complete the mission item.', 'error') } }}>Complete item</Button>}
                    </div>
                  </div>
                </div>
              ))}
            </div>


          </div>
        )}
      </div>

      {/* AI Messages */}
      {messages.length > 0 && (
        <div className="mb-8">
          <h2 className="text-lg font-semibold mb-4">Recent Insights</h2>
          <div className="space-y-3">
            {messages.map((msg) => (
              <div
                key={msg.id}
                className={`bg-card border rounded-lg p-4 shadow-sm ${
                  !msg.isRead ? 'border-primary/50' : ''
                }`}
              >
                <div className="flex items-start gap-3">
                  {msg.messageType === 'warning' ? (
                    <AlertTriangle className="h-5 w-5 text-yellow-500 mt-0.5" />
                  ) : (
                    <Lightbulb className="h-5 w-5 text-primary mt-0.5" />
                  )}
                  <div className="flex-1">
                    <p className="text-sm font-medium capitalize text-muted-foreground mb-1">
                      {msg.messageType}
                    </p>
                    <p className="text-sm">{msg.content}</p>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <CoachConversation />
    </div>
  )
}
