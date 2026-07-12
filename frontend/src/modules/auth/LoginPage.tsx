import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '@/lib/api'
import { useAuthStore } from '@/hooks/useAuthStore'
import { Button } from '@/components/ui/button'
import { Loader2 } from 'lucide-react'

export default function LoginPage() {
  const navigate = useNavigate()
  const setUser = useAuthStore((s) => s.setUser)
  const [usernameOrEmail, setUsernameOrEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setIsLoading(true)

    try {
      const { data } = await api.post('/auth/login', { usernameOrEmail, password })
      localStorage.setItem('accessToken', data.accessToken)
      localStorage.setItem('refreshToken', data.refreshToken)
      setUser(data.user)
      navigate('/')
    } catch (err: any) {
      setError(err.response?.data?.detail || 'Login failed')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="w-full max-w-md p-8 space-y-6 bg-card rounded-lg border shadow-sm">
        <div className="space-y-2 text-center">
          <h1 className="text-2xl font-bold tracking-tight">LifeOS</h1>
          <p className="text-muted-foreground">Sign in to your account</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="username" className="text-sm font-medium">
              Username or Email
            </label>
            <input
              id="username"
              type="text"
              value={usernameOrEmail}
              onChange={(e) => setUsernameOrEmail(e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              required
            />
          </div>

          <div className="space-y-2">
            <label htmlFor="password" className="text-sm font-medium">
              Password
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              required
            />
          </div>

          {error && (
            <div className="text-sm text-destructive bg-destructive/10 p-3 rounded-md">
              {error}
            </div>
          )}

          <Button type="submit" className="w-full" disabled={isLoading}>
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Signing in...
              </>
            ) : (
              'Sign In'
            )}
          </Button>
        </form>

        <p className="text-center text-sm text-muted-foreground">
          Don't have an account?{' '}
          <a href="/register" className="underline text-primary">
            Create one
          </a>
        </p>
      </div>
    </div>
  )
}
