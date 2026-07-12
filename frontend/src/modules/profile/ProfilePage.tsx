import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Save } from 'lucide-react'

interface ProfileData {
  fullName: string
  phone: string
  location: string
  linkedInUrl: string
  gitHubUrl: string
  portfolioUrl: string
  summary: string
  targetRoles: string
  avatarUrl: string
}

export default function ProfilePage() {
  const [profile, setProfile] = useState<ProfileData>({
    fullName: '',
    phone: '',
    location: '',
    linkedInUrl: '',
    gitHubUrl: '',
    portfolioUrl: '',
    summary: '',
    targetRoles: '',
    avatarUrl: '',
  })
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [message, setMessage] = useState('')

  useEffect(() => {
    api.get('/profile')
      .then(({ data }) => {
        if (data) setProfile(data)
      })
      .catch(() => setMessage('Could not load profile'))
      .finally(() => setIsLoading(false))
  }, [])

  const handleChange = (field: keyof ProfileData, value: string) => {
    setProfile((prev) => ({ ...prev, [field]: value }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsSaving(true)
    setMessage('')
    try {
      await api.put('/profile', profile)
      setMessage('Profile saved successfully')
    } catch {
      setMessage('Failed to save profile')
    } finally {
      setIsSaving(false)
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
    <div className="p-8 max-w-3xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">Profile</h1>
        <p className="text-muted-foreground mt-1">Update your personal information</p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6 bg-card border rounded-lg p-6 shadow-sm">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="space-y-2">
            <label className="text-sm font-medium">Full Name</label>
            <input
              type="text"
              value={profile.fullName}
              onChange={(e) => handleChange('fullName', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="John Doe"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Phone</label>
            <input
              type="text"
              value={profile.phone}
              onChange={(e) => handleChange('phone', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="(555) 123-4567"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Location</label>
            <input
              type="text"
              value={profile.location}
              onChange={(e) => handleChange('location', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="Los Angeles, CA"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Target Roles</label>
            <input
              type="text"
              value={profile.targetRoles}
              onChange={(e) => handleChange('targetRoles', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="Software Engineer, Backend Developer"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">LinkedIn URL</label>
            <input
              type="url"
              value={profile.linkedInUrl}
              onChange={(e) => handleChange('linkedInUrl', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="https://linkedin.com/in/johndoe"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">GitHub URL</label>
            <input
              type="url"
              value={profile.gitHubUrl}
              onChange={(e) => handleChange('gitHubUrl', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="https://github.com/johndoe"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Portfolio URL</label>
            <input
              type="url"
              value={profile.portfolioUrl}
              onChange={(e) => handleChange('portfolioUrl', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="https://johndoe.dev"
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">Avatar URL</label>
            <input
              type="url"
              value={profile.avatarUrl}
              onChange={(e) => handleChange('avatarUrl', e.target.value)}
              className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              placeholder="https://example.com/avatar.jpg"
            />
          </div>
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">Professional Summary</label>
          <textarea
            value={profile.summary}
            onChange={(e) => handleChange('summary', e.target.value)}
            className="w-full px-3 py-2 rounded-md border bg-background text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring min-h-[100px]"
            placeholder="Brief summary for your resume..."
          />
        </div>

        {message && (
          <div className={`text-sm p-3 rounded-md ${message.includes('success') ? 'bg-green-500/10 text-green-500' : 'bg-destructive/10 text-destructive'}`}>
            {message}
          </div>
        )}

        <div className="flex justify-end">
          <Button type="submit" disabled={isSaving}>
            {isSaving ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Saving...
              </>
            ) : (
              <>
                <Save className="mr-2 h-4 w-4" />
                Save Profile
              </>
            )}
          </Button>
        </div>
      </form>
    </div>
  )
}
