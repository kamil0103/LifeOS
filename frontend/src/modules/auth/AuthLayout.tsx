import { useEffect } from 'react'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { useAuthStore } from '@/hooks/useAuthStore'
import api from '@/lib/api'
import { Loader2 } from 'lucide-react'

export default function AuthLayout() {
  const { isAuthenticated, isLoading, setUser, logout } = useAuthStore()
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    const initAuth = async () => {
      const token = localStorage.getItem('accessToken')
      if (!token) {
        useAuthStore.getState().setLoading(false)
        return
      }

      try {
        const { data } = await api.get('/auth/me')
        setUser(data.user)
      } catch {
        logout()
      }
    }

    initAuth()
  }, [setUser, logout])

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

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return <Outlet />
}
