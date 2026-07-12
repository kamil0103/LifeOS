import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Bell, Check, Trash2, Info, AlertTriangle, CheckCircle2 } from 'lucide-react'

interface Notification {
  id: string
  title: string
  message: string
  type: string
  actionUrl?: string
  isRead: boolean
  createdAt: string
}

const TYPE_ICONS: Record<string, React.ReactNode> = {
  info: <Info className="h-4 w-4 text-blue-500" />,
  warning: <AlertTriangle className="h-4 w-4 text-yellow-500" />,
  success: <CheckCircle2 className="h-4 w-4 text-green-500" />,
  habit: <Check className="h-4 w-4 text-green-500" />,
  mission: <Info className="h-4 w-4 text-purple-500" />,
}

const TYPE_COLORS: Record<string, string> = {
  info: 'border-l-blue-500',
  warning: 'border-l-yellow-500',
  success: 'border-l-green-500',
  habit: 'border-l-green-500',
  mission: 'border-l-purple-500',
}

export default function NotificationsPage() {
  const [notifications, setNotifications] = useState<Notification[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [filter, setFilter] = useState<'all' | 'unread'>('all')

  useEffect(() => {
    loadNotifications()
  }, [filter])

  const loadNotifications = async () => {
    setIsLoading(true)
    try {
      const params = filter === 'unread' ? '?unread=true' : ''
      const { data } = await api.get(`/notifications${params}`)
      setNotifications(data)
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const markRead = async (id: string) => {
    try {
      await api.post(`/notifications/${id}/read`)
      setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n))
    } catch (err) {
      console.error(err)
    }
  }

  const markAllRead = async () => {
    try {
      await api.post('/notifications/read-all')
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })))
    } catch (err) {
      console.error(err)
    }
  }

  const deleteNotification = async (id: string) => {
    try {
      await api.delete(`/notifications/${id}`)
      setNotifications(prev => prev.filter(n => n.id !== id))
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
    <div className="p-8 max-w-3xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Bell className="h-6 w-6 text-primary" />
          Notifications
        </h1>
        <div className="flex gap-2">
          <Button size="sm" variant={filter === 'all' ? 'default' : 'outline'} onClick={() => setFilter('all')}>All</Button>
          <Button size="sm" variant={filter === 'unread' ? 'default' : 'outline'} onClick={() => setFilter('unread')}>Unread</Button>
        </div>
      </div>

      {notifications.some(n => !n.isRead) && (
        <Button size="sm" variant="outline" className="mb-4" onClick={markAllRead}>
          <Check className="mr-2 h-4 w-4" />
          Mark all read
        </Button>
      )}

      <div className="space-y-3">
        {notifications.length === 0 ? (
          <div className="bg-card border rounded-lg p-8 text-center">
            <Bell className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-muted-foreground">No notifications yet.</p>
          </div>
        ) : (
          notifications.map(n => (
            <div
              key={n.id}
              className={`bg-card border rounded-lg p-4 flex items-start gap-3 ${!n.isRead ? TYPE_COLORS[n.type] || 'border-l-primary' : ''} ${!n.isRead ? 'border-l-4' : ''}`}
            >
              <div className="shrink-0 mt-0.5">{TYPE_ICONS[n.type] || TYPE_ICONS.info}</div>
              <div className="flex-1 min-w-0">
                <p className={`font-medium ${!n.isRead ? '' : 'text-muted-foreground'}`}>{n.title}</p>
                <p className="text-sm text-muted-foreground">{n.message}</p>
                <p className="text-xs text-muted-foreground mt-1">{new Date(n.createdAt).toLocaleString()}</p>
              </div>
              <div className="flex gap-1 shrink-0">
                {!n.isRead && (
                  <Button size="sm" variant="ghost" onClick={() => markRead(n.id)}>
                    <Check className="h-4 w-4" />
                  </Button>
                )}
                <Button size="sm" variant="ghost" className="text-destructive" onClick={() => deleteNotification(n.id)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
