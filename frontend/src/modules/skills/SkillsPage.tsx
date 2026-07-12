import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2 } from 'lucide-react'

interface Skill {
  id: string
  name: string
  category: string
  proficiency: string
  source?: string
}

interface Certificate {
  id: string
  name: string
  issuer?: string
  dateObtained?: string
  expiry?: string
  credentialId?: string
  url?: string
  description?: string
}

const categories = [
  'Programming Language', 'Framework', 'Tool', 'Concept', 'Database', 'Cloud',
  'DevOps', 'Data Science', 'Machine Learning', 'Web Development',
  'Mobile Development', 'Security', 'Algorithm', 'Theory', 'Soft Skill', 'Other'
]

const proficiencies = ['Beginner', 'Intermediate', 'Advanced', 'Expert']

export default function SkillsPage() {
  const [skills, setSkills] = useState<Skill[]>([])
  const [certificates, setCertificates] = useState<Certificate[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [activeTab, setActiveTab] = useState<'skills' | 'certificates'>('skills')

  const [showAddSkill, setShowAddSkill] = useState(false)
  const [newSkill, setNewSkill] = useState({ name: '', category: 'Programming Language', proficiency: 'Beginner', source: '' })

  const [showAddCert, setShowAddCert] = useState(false)
  const [newCert, setNewCert] = useState({ name: '', issuer: '', dateObtained: '', expiry: '', credentialId: '', url: '', description: '' })

  useEffect(() => {
    loadData()
  }, [])

  const loadData = () => {
    setIsLoading(true)
    Promise.all([
      api.get('/skills').then(({ data }) => setSkills(data)).catch(() => {}),
      api.get('/skills/certificates').then(({ data }) => setCertificates(data)).catch(() => {}),
    ]).finally(() => setIsLoading(false))
  }

  const addSkill = async () => {
    if (!newSkill.name) return
    await api.post('/skills', newSkill)
    setNewSkill({ name: '', category: 'Programming Language', proficiency: 'Beginner', source: '' })
    setShowAddSkill(false)
    loadData()
  }

  const addCert = async () => {
    if (!newCert.name) return
    await api.post('/skills/certificates', newCert)
    setNewCert({ name: '', issuer: '', dateObtained: '', expiry: '', credentialId: '', url: '', description: '' })
    setShowAddCert(false)
    loadData()
  }

  const deleteSkill = async (id: string) => {
    if (!confirm('Delete this skill?')) return
    await api.delete(`/skills/${id}`)
    loadData()
  }

  const deleteCert = async (id: string) => {
    if (!confirm('Delete this certificate?')) return
    await api.delete(`/skills/certificates/${id}`)
    loadData()
  }

  const groupedSkills = skills.reduce((acc, skill) => {
    if (!acc[skill.category]) acc[skill.category] = []
    acc[skill.category].push(skill)
    return acc
  }, {} as Record<string, Skill[]>)

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-5xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight">Skills & Certificates</h1>
        <p className="text-muted-foreground mt-1">Manage your technical skills and certifications</p>
      </div>

      <div className="flex gap-4 mb-6">
        <button
          onClick={() => setActiveTab('skills')}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            activeTab === 'skills' ? 'bg-primary text-primary-foreground' : 'bg-secondary hover:bg-secondary/80'
          }`}
        >
          Skills ({skills.length})
        </button>
        <button
          onClick={() => setActiveTab('certificates')}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            activeTab === 'certificates' ? 'bg-primary text-primary-foreground' : 'bg-secondary hover:bg-secondary/80'
          }`}
        >
          Certificates ({certificates.length})
        </button>
      </div>

      {activeTab === 'skills' && (
        <div className="space-y-6">
          <div className="flex justify-end">
            <Button onClick={() => setShowAddSkill(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add Skill
            </Button>
          </div>

          {showAddSkill && (
            <div className="bg-card border rounded-lg p-6 shadow-sm">
              <h3 className="text-lg font-semibold mb-4">Add Skill</h3>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <input placeholder="Skill Name" value={newSkill.name} onChange={(e) => setNewSkill({ ...newSkill, name: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <select value={newSkill.category} onChange={(e) => setNewSkill({ ...newSkill, category: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm">
                  {categories.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
                <select value={newSkill.proficiency} onChange={(e) => setNewSkill({ ...newSkill, proficiency: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm">
                  {proficiencies.map(p => <option key={p} value={p}>{p}</option>)}
                </select>
                <input placeholder="Source (e.g. Course or Project)" value={newSkill.source} onChange={(e) => setNewSkill({ ...newSkill, source: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm md:col-span-3" />
              </div>
              <div className="mt-4 flex gap-2">
                <Button onClick={addSkill}>Save</Button>
                <Button variant="outline" onClick={() => setShowAddSkill(false)}>Cancel</Button>
              </div>
            </div>
          )}

          {Object.keys(groupedSkills).length === 0 && (
            <div className="text-center py-12 text-muted-foreground">No skills yet. Add them manually or upload a transcript.</div>
          )}

          {Object.entries(groupedSkills).map(([category, catSkills]) => (
            <div key={category} className="bg-card border rounded-lg p-6 shadow-sm">
              <h3 className="text-sm font-medium text-muted-foreground uppercase tracking-wider mb-4">{category}</h3>
              <div className="flex flex-wrap gap-2">
                {catSkills.map((skill) => (
                  <div key={skill.id} className="flex items-center gap-2 bg-accent px-3 py-2 rounded-md">
                    <span className="text-sm font-medium">{skill.name}</span>
                    <span className="text-xs text-muted-foreground">{skill.proficiency}</span>
                    <button onClick={() => deleteSkill(skill.id)} className="text-destructive hover:text-destructive/80">
                      <Trash2 className="h-3 w-3" />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {activeTab === 'certificates' && (
        <div className="space-y-6">
          <div className="flex justify-end">
            <Button onClick={() => setShowAddCert(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add Certificate
            </Button>
          </div>

          {showAddCert && (
            <div className="bg-card border rounded-lg p-6 shadow-sm">
              <h3 className="text-lg font-semibold mb-4">Add Certificate</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <input placeholder="Certificate Name" value={newCert.name} onChange={(e) => setNewCert({ ...newCert, name: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Issuing Organization" value={newCert.issuer} onChange={(e) => setNewCert({ ...newCert, issuer: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Date Obtained (YYYY-MM)" value={newCert.dateObtained} onChange={(e) => setNewCert({ ...newCert, dateObtained: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Expiry (YYYY-MM)" value={newCert.expiry} onChange={(e) => setNewCert({ ...newCert, expiry: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Credential ID" value={newCert.credentialId} onChange={(e) => setNewCert({ ...newCert, credentialId: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <input placeholder="Verification URL" value={newCert.url} onChange={(e) => setNewCert({ ...newCert, url: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <textarea placeholder="Description" value={newCert.description} onChange={(e) => setNewCert({ ...newCert, description: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm min-h-[80px] md:col-span-2" />
              </div>
              <div className="mt-4 flex gap-2">
                <Button onClick={addCert}>Save</Button>
                <Button variant="outline" onClick={() => setShowAddCert(false)}>Cancel</Button>
              </div>
            </div>
          )}

          {certificates.length === 0 && (
            <div className="text-center py-12 text-muted-foreground">No certificates yet.</div>
          )}

          {certificates.map((cert) => (
            <div key={cert.id} className="bg-card border rounded-lg p-6 shadow-sm">
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="font-semibold">{cert.name}</h3>
                  <p className="text-sm text-muted-foreground">{cert.issuer}</p>
                  <p className="text-sm text-muted-foreground">
                    Obtained: {cert.dateObtained}
                    {cert.expiry && ` · Expires: ${cert.expiry}`}
                  </p>
                  {cert.credentialId && <p className="text-sm text-muted-foreground">ID: {cert.credentialId}</p>}
                </div>
                <div className="flex gap-2">
                  {cert.url && (
                    <a href={cert.url} target="_blank" rel="noopener noreferrer" className="text-sm text-primary hover:underline">
                      Verify
                    </a>
                  )}
                  <Button variant="ghost" size="sm" onClick={() => deleteCert(cert.id)}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              </div>
              {cert.description && <p className="mt-3 text-sm text-muted-foreground">{cert.description}</p>}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
