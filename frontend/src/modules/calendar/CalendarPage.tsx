import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, ChevronLeft, ChevronRight, Plus, Calendar as CalendarIcon } from 'lucide-react'

interface CalendarEvent {
  id: string
  title: string
  description?: string
  startTime: string
  endTime?: string
  isAllDay: boolean
  location?: string
  eventType: string
  color?: string
  habitName?: string
  jobTitle?: string
}

const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const TYPE_COLORS: Record<string, string> = {
  general: 'bg-blue-500',
  habit: 'bg-green-500',
  job: 'bg-purple-500',
  coding: 'bg-orange-500',
  meeting: 'bg-red-500',
}

export default function CalendarPage() {
  const [currentDate, setCurrentDate] = useState(new Date())
  const [events, setEvents] = useState<CalendarEvent[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [selectedDay, setSelectedDay] = useState<Date | null>(null)

  const [formData, setFormData] = useState({
    title: '',
    description: '',
    startTime: '',
    endTime: '',
    isAllDay: false,
    eventType: 'general',
    color: ''
  })

  useEffect(() => {
    loadEvents()
  }, [currentDate])

  const loadEvents = async () => {
    setIsLoading(true)
    try {
      const start = new Date(currentDate.getFullYear(), currentDate.getMonth(), 1)
      const end = new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0, 23, 59, 59)
      const { data } = await api.get(`/calendar/events?start=${start.toISOString()}&end=${end.toISOString()}`)
      setEvents(data)
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const createEvent = async () => {
    if (!formData.title.trim()) return
    try {
      await api.post('/calendar/events', {
        ...formData,
        startTime: new Date(formData.startTime).toISOString(),
        endTime: formData.endTime ? new Date(formData.endTime).toISOString() : null
      })
      setShowForm(false)
      setFormData({ title: '', description: '', startTime: '', endTime: '', isAllDay: false, eventType: 'general', color: '' })
      loadEvents()
    } catch (err) {
      console.error(err)
    }
  }

  const monthStart = new Date(currentDate.getFullYear(), currentDate.getMonth(), 1)
  const monthEnd = new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0)
  const startOffset = monthStart.getDay()
  const daysInMonth = monthEnd.getDate()

  const prevMonth = () => setCurrentDate(new Date(currentDate.getFullYear(), currentDate.getMonth() - 1, 1))
  const nextMonth = () => setCurrentDate(new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 1))

  const getEventsForDay = (day: number) => {
    const dayStart = new Date(currentDate.getFullYear(), currentDate.getMonth(), day)
    const dayEnd = new Date(currentDate.getFullYear(), currentDate.getMonth(), day, 23, 59, 59)
    return events.filter(e => {
      const eStart = new Date(e.startTime)
      return eStart >= dayStart && eStart <= dayEnd
    })
  }

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-5xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <CalendarIcon className="h-6 w-6 text-primary" />
          Calendar
        </h1>
        <Button onClick={() => { setShowForm(true); setSelectedDay(new Date()) }}>
          <Plus className="mr-2 h-4 w-4" />
          Add Event
        </Button>
      </div>

      {/* Calendar Header */}
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold">
          {currentDate.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })}
        </h2>
        <div className="flex gap-1">
          <Button size="sm" variant="outline" onClick={prevMonth}><ChevronLeft className="h-4 w-4" /></Button>
          <Button size="sm" variant="outline" onClick={() => setCurrentDate(new Date())}>Today</Button>
          <Button size="sm" variant="outline" onClick={nextMonth}><ChevronRight className="h-4 w-4" /></Button>
        </div>
      </div>

      {/* Calendar Grid */}
      <div className="border rounded-lg overflow-hidden">
        {/* Day headers */}
        <div className="grid grid-cols-7 bg-muted">
          {DAYS.map(day => (
            <div key={day} className="p-2 text-center text-sm font-medium">{day}</div>
          ))}
        </div>

        {/* Days */}
        <div className="grid grid-cols-7">
          {Array.from({ length: startOffset }).map((_, i) => (
            <div key={`empty-${i}`} className="min-h-[100px] border-b border-r bg-muted/30" />
          ))}
          {Array.from({ length: daysInMonth }).map((_, i) => {
            const day = i + 1
            const dayEvents = getEventsForDay(day)
            const isToday = new Date().toDateString() === new Date(currentDate.getFullYear(), currentDate.getMonth(), day).toDateString()
            return (
              <div
                key={day}
                className={`min-h-[100px] border-b border-r p-1 cursor-pointer hover:bg-accent/50 ${isToday ? 'bg-primary/5' : ''}`}
                onClick={() => { setSelectedDay(new Date(currentDate.getFullYear(), currentDate.getMonth(), day)); setShowForm(true) }}
              >
                <span className={`text-sm font-medium ${isToday ? 'text-primary' : ''}`}>{day}</span>
                <div className="mt-1 space-y-0.5">
                  {dayEvents.slice(0, 3).map(e => (
                    <div
                      key={e.id}
                      className={`text-[10px] px-1 py-0.5 rounded text-white truncate ${TYPE_COLORS[e.eventType] || 'bg-gray-500'}`}
                      onClick={ev => { ev.stopPropagation(); }}
                    >
                      {e.title}
                    </div>
                  ))}
                  {dayEvents.length > 3 && (
                    <div className="text-[10px] text-muted-foreground">+{dayEvents.length - 3} more</div>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      </div>

      {/* Event Form Modal */}
      {showForm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-card border rounded-lg p-6 w-[500px] max-h-[90vh] overflow-y-auto">
            <h3 className="font-semibold mb-4">
              {selectedDay ? selectedDay.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) : 'New Event'}
            </h3>
            <div className="space-y-3">
              <div>
                <label className="text-sm font-medium mb-1 block">Title</label>
                <input type="text" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.title} onChange={e => setFormData({ ...formData, title: e.target.value })} />
              </div>
              <div>
                <label className="text-sm font-medium mb-1 block">Description</label>
                <textarea className="w-full px-3 py-2 border rounded-md bg-background text-sm" rows={2} value={formData.description} onChange={e => setFormData({ ...formData, description: e.target.value })} />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-sm font-medium mb-1 block">Start</label>
                  <input type="datetime-local" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.startTime} onChange={e => setFormData({ ...formData, startTime: e.target.value })} />
                </div>
                <div>
                  <label className="text-sm font-medium mb-1 block">End</label>
                  <input type="datetime-local" className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.endTime} onChange={e => setFormData({ ...formData, endTime: e.target.value })} />
                </div>
              </div>
              <div>
                <label className="text-sm font-medium mb-1 block">Type</label>
                <select className="w-full px-3 py-2 border rounded-md bg-background text-sm" value={formData.eventType} onChange={e => setFormData({ ...formData, eventType: e.target.value })}>
                  <option value="general">General</option>
                  <option value="habit">Habit</option>
                  <option value="job">Job</option>
                  <option value="coding">Coding</option>
                  <option value="meeting">Meeting</option>
                </select>
              </div>
            </div>
            <div className="flex gap-2 mt-4 justify-end">
              <Button variant="outline" onClick={() => setShowForm(false)}>Cancel</Button>
              <Button onClick={createEvent}>Save</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
