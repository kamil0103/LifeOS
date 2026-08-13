import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Pencil, BookOpen, Cloud, Settings, X, CheckCircle, AlertCircle, RefreshCw } from 'lucide-react'

interface JournalEntry {
  id: string
  title: string
  content: string
  entryDate: string
  mood?: string
  updatedAt: string
}

interface JournalSettings {
  googleDocId?: string
  hasServiceAccount: boolean
  serviceAccountEmail?: string
  autoSync: boolean
  lastSyncAt?: string
}

const MOODS = ['😊 Great', '🙂 Good', '😐 Okay', '😕 Down', '😢 Bad']

export default function JournalPage() {
  const [entries, setEntries] = useState<JournalEntry[]>([])
  const [settings, setSettings] = useState<JournalSettings | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const [showEditor, setShowEditor] = useState(false)
  const [editEntry, setEditEntry] = useState<JournalEntry | null>(null)
  const [form, setForm] = useState({ title: '', content: '', entryDate: '', mood: '' })

  const [showSettings, setShowSettings] = useState(false)
  const [settingsForm, setSettingsForm] = useState({ googleDocId: '', serviceAccountJson: '', autoSync: false })
  const [syncStatus, setSyncStatus] = useState<{ ok: boolean; msg: string } | null>(null)
  const [isSyncing, setIsSyncing] = useState(false)
  const [isTesting, setIsTesting] = useState(false)

  useEffect(() => {
    loadAll()
  }, [])

  const loadAll = async () => {
    setIsLoading(true)
    try {
      const [entriesRes, settingsRes] = await Promise.all([
        api.get('/journal'),
        api.get('/journal/settings'),
      ])
      setEntries(entriesRes.data)
      setSettings(settingsRes.data)
      setSettingsForm(f => ({ ...f, googleDocId: settingsRes.data.googleDocId || '', autoSync: settingsRes.data.autoSync || false }))
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const openNew = () => {
    setEditEntry(null)
    setForm({ title: '', content: '', entryDate: new Date().toISOString().slice(0, 10), mood: '' })
    setShowEditor(true)
  }

  const openEdit = (entry: JournalEntry) => {
    setEditEntry(entry)
    setForm({
      title: entry.title,
      content: entry.content,
      entryDate: entry.entryDate.slice(0, 10),
      mood: entry.mood || '',
    })
    setShowEditor(true)
  }

  const saveEntry = async () => {
    if (!form.title.trim()) return
    try {
      if (editEntry) {
        await api.put(`/journal/${editEntry.id}`, form)
      } else {
        await api.post('/journal', form)
      }
      setShowEditor(false)
      loadAll()
    } catch (err) {
      console.error(err)
      alert('Failed to save entry')
    }
  }

  const deleteEntry = async (id: string) => {
    if (!confirm('Delete this journal entry?')) return
    try {
      await api.delete(`/journal/${id}`)
      loadAll()
    } catch (err) {
      console.error(err)
    }
  }

  const saveSettings = async () => {
    setSyncStatus(null)
    try {
      const { data } = await api.put('/journal/settings', settingsForm)
      setSettings(data)
      setSettingsForm(f => ({ ...f, serviceAccountJson: '' }))
      setSyncStatus({ ok: true, msg: 'Settings saved' })
    } catch (err: any) {
      setSyncStatus({ ok: false, msg: err?.response?.data?.detail || 'Failed to save settings' })
    }
  }

  const testConnection = async () => {
    setIsTesting(true)
    setSyncStatus(null)
    try {
      const { data } = await api.post('/journal/sync/test')
      setSyncStatus({ ok: true, msg: `Connected to "${data.documentTitle}"` })
    } catch (err: any) {
      setSyncStatus({ ok: false, msg: err?.response?.data?.detail || 'Connection failed' })
    } finally {
      setIsTesting(false)
    }
  }

  const syncNow = async () => {
    setIsSyncing(true)
    setSyncStatus(null)
    try {
      const { data } = await api.post('/journal/sync')
      setSyncStatus({ ok: true, msg: `Synced ${data.entriesSynced} entries` })
      loadAll()
    } catch (err: any) {
      setSyncStatus({ ok: false, msg: err?.response?.data?.detail || 'Sync failed' })
    } finally {
      setIsSyncing(false)
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
    <div className="p-8 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <BookOpen className="h-6 w-6 text-primary" />
            Journal
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {settings?.hasServiceAccount && settings?.googleDocId
              ? `Backing up to Google Docs${settings.lastSyncAt ? ` · last synced ${new Date(settings.lastSyncAt).toLocaleString()}` : ''}`
              : 'Private journal with optional Google Docs backup'}
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => setShowSettings(true)}>
            <Settings className="mr-2 h-4 w-4" />
            Backup Settings
          </Button>
          {settings?.hasServiceAccount && settings?.googleDocId && (
            <Button variant="outline" onClick={syncNow} disabled={isSyncing}>
              {isSyncing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
              Sync Now
            </Button>
          )}
          <Button onClick={openNew}>
            <Plus className="mr-2 h-4 w-4" />
            New Entry
          </Button>
        </div>
      </div>

      {syncStatus && (
        <div className={`mb-4 text-sm p-3 rounded-md flex items-center gap-2 ${syncStatus.ok ? 'bg-green-500/10 text-green-500' : 'bg-destructive/10 text-destructive'}`}>
          {syncStatus.ok ? <CheckCircle className="h-4 w-4" /> : <AlertCircle className="h-4 w-4" />}
          {syncStatus.msg}
        </div>
      )}

      {/* Editor Modal */}
      {showEditor && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setShowEditor(false)}>
          <div className="bg-card border rounded-lg p-6 w-[640px] max-h-[85vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h3 className="text-lg font-semibold mb-4">{editEntry ? 'Edit Entry' : 'New Entry'}</h3>
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <input
                  type="date"
                  value={form.entryDate}
                  onChange={e => setForm({ ...form, entryDate: e.target.value })}
                  className="px-3 py-2 rounded-md border bg-background text-sm"
                />
                <select
                  value={form.mood}
                  onChange={e => setForm({ ...form, mood: e.target.value })}
                  className="px-3 py-2 rounded-md border bg-background text-sm"
                >
                  <option value="">Mood (optional)</option>
                  {MOODS.map(m => <option key={m} value={m}>{m}</option>)}
                </select>
              </div>
              <input
                type="text"
                placeholder="Entry title"
                value={form.title}
                onChange={e => setForm({ ...form, title: e.target.value })}
                className="w-full px-3 py-2 rounded-md border bg-background text-sm"
              />
              <textarea
                placeholder="Write your thoughts..."
                value={form.content}
                onChange={e => setForm({ ...form, content: e.target.value })}
                className="w-full px-3 py-2 rounded-md border bg-background text-sm min-h-[220px]"
              />
            </div>
            <div className="flex gap-2 mt-4 justify-end">
              <Button variant="outline" onClick={() => setShowEditor(false)}>Cancel</Button>
              <Button onClick={saveEntry}>Save Entry</Button>
            </div>
          </div>
        </div>
      )}

      {/* Settings Modal */}
      {showSettings && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setShowSettings(false)}>
          <div className="bg-card border rounded-lg p-6 w-[680px] max-h-[88vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold flex items-center gap-2">
                <Cloud className="h-5 w-5 text-primary" />
                Google Docs Backup
              </h3>
              <Button variant="ghost" size="sm" onClick={() => setShowSettings(false)}><X className="h-4 w-4" /></Button>
            </div>

            {/* How-to guide */}
            <div className="bg-secondary/40 rounded-md p-4 mb-5 text-sm space-y-2">
              <p className="font-medium">One-time setup (~5 minutes):</p>
              <ol className="list-decimal pl-5 space-y-1.5 text-muted-foreground">
                <li>Go to <a href="https://console.cloud.google.com" target="_blank" rel="noopener noreferrer" className="text-primary underline">Google Cloud Console</a> → create a project (or pick an existing one).</li>
                <li>Enable the <strong>Google Docs API</strong> (APIs &amp; Services → Library → search "Google Docs API" → Enable).</li>
                <li>Go to <strong>IAM &amp; Admin → Service Accounts</strong> → Create Service Account → name it e.g. <code>lifeos-journal</code>.</li>
                <li>On the service account → <strong>Keys</strong> → Add Key → Create new key → <strong>JSON</strong> → download the file.</li>
                <li>Create a new <a href="https://docs.google.com" target="_blank" rel="noopener noreferrer" className="text-primary underline">Google Doc</a> (this is your backup doc).</li>
                <li>In the Doc, click <strong>Share</strong> → paste the service account's <strong>email</strong> (looks like <code>name@project.iam.gserviceaccount.com</code>) → give <strong>Editor</strong> access.</li>
                <li>Copy the Doc's <strong>ID</strong> from its URL (the long string between <code>/d/</code> and <code>/edit</code>).</li>
                <li>Paste the <strong>Doc ID</strong> and the full contents of the downloaded <strong>JSON key file</strong> below.</li>
              </ol>
              {settings?.serviceAccountEmail && (
                <p className="text-xs pt-1">Connected service account: <code className="text-primary">{settings.serviceAccountEmail}</code></p>
              )}
            </div>

            <div className="space-y-3">
              <div>
                <label className="text-sm font-medium mb-1 block">Google Doc ID</label>
                <input
                  type="text"
                  placeholder="e.g. 1a2B3cD4eF5gH6iJ7kL8mN9oP0qR..."
                  value={settingsForm.googleDocId}
                  onChange={e => setSettingsForm({ ...settingsForm, googleDocId: e.target.value.trim() })}
                  className="w-full px-3 py-2 rounded-md border bg-background text-sm font-mono"
                />
              </div>
              <div>
                <label className="text-sm font-medium mb-1 block">
                  Service Account JSON Key {settings?.hasServiceAccount && <span className="text-green-500 text-xs">(saved — paste a new one to replace)</span>}
                </label>
                <textarea
                  placeholder='{"type": "service_account", "project_id": "...", "client_email": "...", ...}'
                  value={settingsForm.serviceAccountJson}
                  onChange={e => setSettingsForm({ ...settingsForm, serviceAccountJson: e.target.value })}
                  className="w-full px-3 py-2 rounded-md border bg-background text-xs font-mono min-h-[120px]"
                />
              </div>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={settingsForm.autoSync}
                  onChange={e => setSettingsForm({ ...settingsForm, autoSync: e.target.checked })}
                  className="h-4 w-4"
                />
                Auto-sync after every change
              </label>
            </div>

            <div className="flex gap-2 mt-5 justify-end">
              <Button variant="outline" onClick={testConnection} disabled={isTesting || !settings?.hasServiceAccount && !settingsForm.serviceAccountJson}>
                {isTesting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Cloud className="mr-2 h-4 w-4" />}
                Test Connection
              </Button>
              <Button onClick={saveSettings}>Save Settings</Button>
            </div>
            <p className="text-xs text-muted-foreground mt-3">
              After saving, use "Test Connection", then "Sync Now" on the Journal page to push all entries into your doc.
            </p>
          </div>
        </div>
      )}

      {/* Entries */}
      <div className="space-y-4">
        {entries.length === 0 ? (
          <div className="bg-card border rounded-lg p-12 text-center">
            <BookOpen className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-muted-foreground">No journal entries yet.</p>
            <p className="text-sm text-muted-foreground mt-1">Write your first entry to get started.</p>
          </div>
        ) : (
          entries.map(entry => (
            <div key={entry.id} className="bg-card border rounded-lg p-5">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-xs text-muted-foreground">
                    {new Date(entry.entryDate).toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
                    {entry.mood && <span className="ml-2">{entry.mood}</span>}
                  </p>
                  <h3 className="font-semibold mt-0.5">{entry.title}</h3>
                </div>
                <div className="flex gap-1 shrink-0">
                  <Button variant="ghost" size="sm" onClick={() => openEdit(entry)}>
                    <Pencil className="h-4 w-4 text-muted-foreground" />
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => deleteEntry(entry.id)}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              </div>
              <p className="text-sm text-muted-foreground mt-2 whitespace-pre-line">{entry.content}</p>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
