import { useEffect, useState } from 'react'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useAuthStore } from '@/hooks/useAuthStore'
import api from '@/lib/api'
import { Loader2 } from 'lucide-react'

export default function AuthLayout() {
  const { isAuthenticated, isLoading, setUser } = useAuthStore()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let active = true
    const initAuth = async () => {
      setError('')
      useAuthStore.getState().setLoading(true)
      const token = localStorage.getItem('accessToken')
      if (!token) { setUser(null); return }
      try {
        const { data } = await api.get('/lifeos/session')
        if (!data?.user?.id) throw new Error('Invalid session response')
        if (active) setUser(data.user)
      } catch {
        if (!active) return
        if (!localStorage.getItem('accessToken')) setUser(null)
        else setError('Could not restore your session. Your login is saved; try again when the connection is available.')
      }
    }
    initAuth()
    return () => { active = false }
  }, [setUser, attempt])

  useEffect(() => {
    if (!isLoading) {
      const publicRoutes = ['/login', '/register']
      const isPublic = publicRoutes.includes(location.pathname)

      if (!isAuthenticated && !isPublic) {
        navigate('/login', { replace: true })
      } else if (isAuthenticated && isPublic) {
        navigate('/', { replace: true })
      }
    }
  }, [isAuthenticated, isLoading, location.pathname, navigate])

  if (error) return <div className="min-h-screen flex items-center justify-center bg-background p-6"><div className="max-w-md space-y-4"><p role="alert">{error}</p><button className="border rounded-md px-4 py-2" onClick={() => setAttempt(value => value + 1)}>Try again</button></div></div>

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return <Outlet />
}
