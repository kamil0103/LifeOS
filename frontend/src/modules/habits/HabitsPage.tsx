import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Check, Trash2, Flame } from 'lucide-react'

interface Habit {
  id: string
  name: string
  category: string
  icon?: string
  color?: string
  targetValue?: number
  unit?: string
  frequency: string
  currentStreak: number
  isCompleted: boolean
}

const categories = [
  'Coding', 'Exercise', 'Bible', 'Prayer', 'Reading', 'Applications', 'Sleep', 'Water', 'Other'
]

export default function HabitsPage() {
  const [habits, setHabits] = useState<Habit[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [showAdd, setShowAdd] = useState(false)
  const [newHabit, setNewHabit] = useState({
    name: '', category: 'Coding', targetValue: '', unit: '', frequency: 'daily', icon: '', color: ''
  })
  const [completingId, setCompletingId] = useState<string | null>(null)
  const [xpFlash, setXpFlash] = useState<{amount: number, habitName: string} | null>(null)

  useEffect(() => {
    loadHabits()
  }, [])

  const loadHabits = () => {
    setIsLoading(true)
    api.get('/habits')
      .then(({ data }) => {
        const transformed = data.map((h: any) => ({
          ...h,
          currentStreak: h.streak?.currentStreak ?? 0,
          isCompleted: false // Will be checked separately
        }))
        setHabits(transformed)
        // Check today's completions
        checkCompletions(transformed)
      })
      .catch(console.error)
      .finally(() => setIsLoading(false))
  }

  const checkCompletions = async (habitList: Habit[]) => {
    for (const habit of habitList) {
      try {
        const { data: completions } = await api.get(`/habits/${habit.id}/completions?days=1`)
        if (completions.length > 0) {
          setHabits(prev => prev.map(h => 
            h.id === habit.id ? { ...h, isCompleted: true } : h
          ))
        }
      } catch (err) {
        console.error(err)
      }
    }
  }

  const addHabit = async () => {
    if (!newHabit.name) return
    try {
      await api.post('/habits', {
        name: newHabit.name,
        category: newHabit.category,
        targetValue: newHabit.targetValue ? parseFloat(newHabit.targetValue) : null,
        unit: newHabit.unit,
        frequency: newHabit.frequency,
        icon: newHabit.icon,
        color: newHabit.color
      })
      setNewHabit({ name: '', category: 'Coding', targetValue: '', unit: '', frequency: 'daily', icon: '', color: '' })
      setShowAdd(false)
      loadHabits()
    } catch (err) {
      console.error(err)
    }
  }

  const completeHabit = async (id: string) => {
    setCompletingId(id)
    try {
      const { data } = await api.post(`/habits/${id}/complete`)
      setHabits(prev => prev.map(h => 
        h.id === id ? { ...h, isCompleted: true, currentStreak: data.currentStreak } : h
      ))
      setXpFlash({ amount: data.xpEarned, habitName: habits.find(h => h.id === id)?.name ?? '' })
      setTimeout(() => setXpFlash(null), 2000)
    } catch (err: any) {
      if (err.response?.status === 400) {
        alert('Already completed today!')
      }
    } finally {
      setCompletingId(null)
    }
  }

  const deleteHabit = async (id: string) => {
    if (!confirm('Delete this habit?')) return
    try {
      await api.delete(`/habits/${id}`)
      loadHabits()
    } catch (err) {
      console.error(err)
    }
  }

  const getCategoryColor = (category: string) => {
    const colors: Record<string, string> = {
      'Coding': 'bg-blue-500/10 text-blue-500',
      'Exercise': 'bg-green-500/10 text-green-500',
      'Bible': 'bg-yellow-500/10 text-yellow-500',
      'Prayer': 'bg-purple-500/10 text-purple-500',
      'Reading': 'bg-orange-500/10 text-orange-500',
      'Applications': 'bg-pink-500/10 text-pink-500',
      'Sleep': 'bg-indigo-500/10 text-indigo-500',
      'Water': 'bg-cyan-500/10 text-cyan-500',
      'Other': 'bg-gray-500/10 text-gray-500',
    }
    return colors[category] || colors['Other']
  }

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  const groupedHabits = habits.reduce((acc, habit) => {
    if (!acc[habit.category]) acc[habit.category] = []
    acc[habit.category].push(habit)
    return acc
  }, {} as Record<string, Habit[]>)

  return (
    <div className="p-8 max-w-5xl mx-auto">
      {/* XP Flash Animation */}
      {xpFlash && (
        <div className="fixed top-20 right-8 bg-primary text-primary-foreground px-4 py-2 rounded-lg shadow-lg animate-bounce z-50">
          +{xpFlash.amount} XP for {xpFlash.habitName}!
        </div>
      )}

      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Habits</h1>
          <p className="text-muted-foreground mt-1">Build consistency, earn XP, level up</p>
        </div>
        <Button onClick={() => setShowAdd(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Add Habit
        </Button>
      </div>

      {showAdd && (
        <div className="bg-card border rounded-lg p-6 shadow-sm mb-6">
          <h3 className="text-lg font-semibold mb-4">Add Habit</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <input
              placeholder="Habit Name (e.g. LeetCode 1 problem)"
              value={newHabit.name}
              onChange={(e) => setNewHabit({ ...newHabit, name: e.target.value })}
              className="px-3 py-2 rounded-md border bg-background text-sm"
            />
            <select
              value={newHabit.category}
              onChange={(e) => setNewHabit({ ...newHabit, category: e.target.value })}
              className="px-3 py-2 rounded-md border bg-background text-sm"
            >
              {categories.map(c => <option key={c} value={c}>{c}</option>)}
            </select>
            <div className="flex gap-2">
              <input
                type="number"
                placeholder="Target"
                value={newHabit.targetValue}
                onChange={(e) => setNewHabit({ ...newHabit, targetValue: e.target.value })}
                className="px-3 py-2 rounded-md border bg-background text-sm w-1/2"
              />
              <input
                placeholder="Unit (min, pages, etc.)"
                value={newHabit.unit}
                onChange={(e) => setNewHabit({ ...newHabit, unit: e.target.value })}
                className="px-3 py-2 rounded-md border bg-background text-sm w-1/2"
              />
            </div>
          </div>
          <div className="mt-4 flex gap-2">
            <Button onClick={addHabit}>Save</Button>
            <Button variant="outline" onClick={() => setShowAdd(false)}>Cancel</Button>
          </div>
        </div>
      )}

      <div className="space-y-6">
        {habits.length === 0 && (
          <div className="text-center py-12 text-muted-foreground">
            No habits yet. Create your first habit to start building streaks.
          </div>
        )}

        {Object.entries(groupedHabits).map(([category, catHabits]) => (
          <div key={category} className="space-y-3">
            <h2 className={`text-sm font-medium px-3 py-1 rounded-md inline-block ${getCategoryColor(category)}`}>
              {category}
            </h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {catHabits.map((habit) => (
                <div
                  key={habit.id}
                  className={`bg-card border rounded-lg p-4 shadow-sm transition-all ${
                    habit.isCompleted ? 'opacity-60' : ''
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex-1">
                      <h3 className={`font-medium ${habit.isCompleted ? 'line-through text-muted-foreground' : ''}`}>
                        {habit.name}
                      </h3>
                      <div className="flex items-center gap-3 mt-1">
                        {habit.currentStreak > 0 && (
                          <span className="flex items-center gap-1 text-xs text-orange-500">
                            <Flame className="h-3 w-3" />
                            {habit.currentStreak} day streak
                          </span>
                        )}
                        {habit.targetValue && (
                          <span className="text-xs text-muted-foreground">
                            Target: {habit.targetValue} {habit.unit}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {habit.isCompleted ? (
                        <div className="h-8 w-8 rounded-full bg-green-500/10 flex items-center justify-center">
                          <Check className="h-4 w-4 text-green-500" />
                        </div>
                      ) : (
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
                      )}
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => deleteHabit(habit.id)}
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
