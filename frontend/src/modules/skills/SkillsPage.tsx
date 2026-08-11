import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Pencil } from 'lucide-react'

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

  const [isExtractingAi, setIsExtractingAi] = useState(false)
  const [extractedModalSkills, setExtractedModalSkills] = useState<Array<{ name: string; category: string; proficiency: string; selected: boolean }>>([])
  const [showExtractModal, setShowExtractModal] = useState(false)

  const extractSkillsAi = async () => {
    setIsExtractingAi(true)
    try {
      const { data } = await api.post('/skills/extract-ai')
      setExtractedModalSkills(data.map((s: any) => ({ ...s, selected: true })))
      setShowExtractModal(true)
    } catch (err) {
      console.error(err)
      alert('AI skill extraction failed')
    } finally {
      setIsExtractingAi(false)
    }
  }

  const saveSelectedExtractedSkills = async (auto: boolean = false) => {
    try {
      const skillsToSave = auto ? extractedModalSkills : extractedModalSkills.filter(s => s.selected)
      await api.post('/skills/save-extracted', skillsToSave)
      setShowExtractModal(false)
      loadData()
      alert('Skills saved successfully!')
    } catch (err) {
      console.error(err)
      alert('Failed to save extracted skills')
    }
  }
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

  // Edit modals
  const [editSkill, setEditSkill] = useState<Skill | null>(null)
  const [editCert, setEditCert] = useState<Certificate | null>(null)

  const saveEditSkill = async () => {
    if (!editSkill || !editSkill.name) return
    await api.put(`/skills/${editSkill.id}`, {
      name: editSkill.name,
      category: editSkill.category,
      proficiency: editSkill.proficiency,
      source: editSkill.source,
    })
    setEditSkill(null)
    loadData()
  }

  const saveEditCert = async () => {
    if (!editCert || !editCert.name) return
    await api.put(`/skills/certificates/${editCert.id}`, {
      name: editCert.name,
      issuer: editCert.issuer,
      dateObtained: editCert.dateObtained,
      expiry: editCert.expiry,
      credentialId: editCert.credentialId,
      url: editCert.url,
      description: editCert.description,
    })
    setEditCert(null)
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
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={extractSkillsAi} disabled={isExtractingAi}>
              {isExtractingAi ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Plus className="mr-2 h-4 w-4" />}
              AI Extract Skills
            </Button>
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
                    <button onClick={() => setEditSkill(skill)} className="text-muted-foreground hover:text-foreground">
                      <Pencil className="h-3 w-3" />
                    </button>
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
                  <Button variant="ghost" size="sm" onClick={() => setEditCert(cert)}>
                    <Pencil className="h-4 w-4 text-muted-foreground" />
                  </Button>
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
      {/* Edit Skill Modal */}
      {editSkill && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setEditSkill(null)}>
          <div className="bg-card border rounded-lg p-6 w-[420px]" onClick={e => e.stopPropagation()}>
            <h3 className="text-lg font-semibold mb-4">Edit Skill</h3>
            <div className="space-y-3">
              <input placeholder="Skill Name" value={editSkill.name} onChange={e => setEditSkill({ ...editSkill, name: e.target.value })} className="w-full px-3 py-2 rounded-md border bg-background text-sm" />
              <select value={editSkill.category} onChange={e => setEditSkill({ ...editSkill, category: e.target.value })} className="w-full px-3 py-2 rounded-md border bg-background text-sm">
                {categories.map(c => <option key={c} value={c}>{c}</option>)}
              </select>
              <select value={editSkill.proficiency} onChange={e => setEditSkill({ ...editSkill, proficiency: e.target.value })} className="w-full px-3 py-2 rounded-md border bg-background text-sm">
                {proficiencies.map(p => <option key={p} value={p}>{p}</option>)}
              </select>
              <input placeholder="Source (e.g. Course or Project)" value={editSkill.source ?? ''} onChange={e => setEditSkill({ ...editSkill, source: e.target.value })} className="w-full px-3 py-2 rounded-md border bg-background text-sm" />
            </div>
            <div className="flex gap-2 mt-5 justify-end">
              <Button variant="outline" onClick={() => setEditSkill(null)}>Cancel</Button>
              <Button onClick={saveEditSkill}>Save</Button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Certificate Modal */}
      {editCert && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setEditCert(null)}>
          <div className="bg-card border rounded-lg p-6 w-[520px] max-h-[85vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h3 className="text-lg font-semibold mb-4">Edit Certificate</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <input placeholder="Certificate Name" value={editCert.name} onChange={e => setEditCert({ ...editCert, name: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Issuing Organization" value={editCert.issuer ?? ''} onChange={e => setEditCert({ ...editCert, issuer: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Date Obtained (YYYY-MM)" value={editCert.dateObtained ?? ''} onChange={e => setEditCert({ ...editCert, dateObtained: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Expiry (YYYY-MM)" value={editCert.expiry ?? ''} onChange={e => setEditCert({ ...editCert, expiry: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Credential ID" value={editCert.credentialId ?? ''} onChange={e => setEditCert({ ...editCert, credentialId: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <input placeholder="Verification URL" value={editCert.url ?? ''} onChange={e => setEditCert({ ...editCert, url: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
              <textarea placeholder="Description" value={editCert.description ?? ''} onChange={e => setEditCert({ ...editCert, description: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm min-h-[80px] md:col-span-2" />
            </div>
            <div className="flex gap-2 mt-5 justify-end">
              <Button variant="outline" onClick={() => setEditCert(null)}>Cancel</Button>
              <Button onClick={saveEditCert}>Save</Button>
            </div>
          </div>
        </div>
      )}

      {/* AI Extraction Modal */}
      {showExtractModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-card border rounded-lg p-6 w-[600px] max-h-[80vh] overflow-y-auto">
            <h3 className="text-lg font-semibold mb-2">Review Extracted Skills</h3>
            <p className="text-sm text-muted-foreground mb-4">Select the skills you want to add to your profile, or edit them.</p>
            <div className="space-y-2 mb-6 max-h-[50vh] overflow-y-auto">
              {extractedModalSkills.map((s, i) => (
                <div key={i} className="flex items-center gap-3 bg-secondary/50 p-3 rounded-md">
                  <input
                    type="checkbox"
                    checked={s.selected}
                    onChange={e => {
                      const next = [...extractedModalSkills]
                      next[i].selected = e.target.checked
                      setExtractedModalSkills(next)
                    }}
                    className="h-4 w-4"
                  />
                  <input
                    type="text"
                    value={s.name}
                    onChange={e => {
                      const next = [...extractedModalSkills]
                      next[i].name = e.target.value
                      setExtractedModalSkills(next)
                    }}
                    className="flex-1 px-2 py-1 border rounded bg-background text-sm font-medium"
                  />
                  <select
                    value={s.category}
                    onChange={e => {
                      const next = [...extractedModalSkills]
                      next[i].category = e.target.value
                      setExtractedModalSkills(next)
                    }}
                    className="px-2 py-1 border rounded bg-background text-sm"
                  >
                    {categories.map(c => <option key={c} value={c}>{c}</option>)}
                  </select>
                </div>
              ))}
            </div>
            <div className="flex gap-2 justify-end">
              <Button variant="outline" onClick={() => setShowExtractModal(false)}>Cancel</Button>
              <Button onClick={() => saveSelectedExtractedSkills(false)}>Save Selected</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
