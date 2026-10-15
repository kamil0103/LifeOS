import { useUnsavedChanges } from '@/hooks/useUnsavedChanges'
import { useAuthStore } from '@/hooks/useAuthStore'
import { useState, useEffect, useRef } from 'react'
import { useRecordActions } from '@/hooks/useRecordActions'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Pencil, BookOpen, Cloud, Settings, X, CheckCircle, AlertCircle, RefreshCw } from 'lucide-react'

interface JournalEntry {
  id: string
  title: string
  content: string
  entryDate: string
  mood?: string
  localDate?: string
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
  const { busy, error, setError, mutate } = useRecordActions()
  const requestId = useRef(crypto.randomUUID())
  const [query, setQuery] = useState('')
  const [entries, setEntries] = useState<JournalEntry[]>([])
  const [settings, setSettings] = useState<JournalSettings | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const [showEditor, setShowEditor] = useState(false)
  const [editEntry, setEditEntry] = useState<JournalEntry | null>(null)
  const [form, setForm] = useState({ title: '', content: '', entryDate: '', mood: '' })

  const [formSnapshot, setFormSnapshot] = useState('')
  const userId = useAuthStore(state => state.user?.id)
  const draftKey = 'lifeos-journal-draft-' + userId
  const dirty = showEditor && JSON.stringify(form) !== formSnapshot
  useUnsavedChanges(dirty)
  useEffect(() => {
    if (dirty) { try { sessionStorage.setItem(draftKey, JSON.stringify({ form, entryId: editEntry?.id, expectedUpdatedAt: editEntry?.updatedAt, requestId: requestId.current })) } catch { /* Keep the in-memory draft. */ } }
  }, [dirty, form, draftKey, editEntry])

  const [showSettings, setShowSettings] = useState(false)
  const [settingsForm, setSettingsForm] = useState({ googleDocId: '', autoSync: false })
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
      setError('Could not load your records or backup settings. Try again.')
    } finally {
      setIsLoading(false)
    }
  }

  const openNew = async () => {
    setEditEntry(null)
    let zone = 'America/Los_Angeles'
    try { const { data } = await api.get('/profile'); zone = data.timeZone || zone } catch { /* Default for new accounts. */ }
    try { new Intl.DateTimeFormat('en-US', { timeZone: zone }).format(new Date()) } catch { zone = 'America/Los_Angeles' }
    const parts = new Intl.DateTimeFormat('en-US', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
    const part = (type: string) => parts.find(p => p.type === type)?.value
    const fresh = { title: '', content: '', entryDate: `${part('year')}-${part('month')}-${part('day')}`, mood: '' }
    setFormSnapshot(JSON.stringify(fresh))
    requestId.current = crypto.randomUUID()
    const cached = sessionStorage.getItem(draftKey)
    if (cached && window.confirm('Restore your unsaved journal draft?')) {
      try {
        const draft = JSON.parse(cached)
        if (draft.form) {
          const original = entries.find(e => e.id === draft.entryId)
          if (draft.entryId && !original && !window.confirm('The original entry was removed. Restore this draft as a new entry?')) return
          setEditEntry(original ? { ...original, updatedAt: draft.expectedUpdatedAt || original.updatedAt } : null)
          requestId.current = draft.requestId || crypto.randomUUID(); setForm(draft.form)
        } else if (window.confirm('Restore this older draft as a new entry?')) setForm(draft)
        else setForm(fresh)
      } catch { setForm(fresh) }
    } else setForm(fresh)
    setShowEditor(true)
  }

  const openEdit = (entry: JournalEntry) => {
    setEditEntry(entry)
    setFormSnapshot(JSON.stringify({ title: entry.title, content: entry.content, entryDate: (entry.localDate || entry.entryDate.slice(0, 10)), mood: entry.mood || '' }))
    setForm({
      title: entry.title,
      content: entry.content,
      entryDate: (entry.localDate || entry.entryDate.slice(0, 10)),
      mood: entry.mood || '',
    })
    setShowEditor(true)
  }

  const saveEntry = async () => {
    if (!form.title.trim()) return
    return mutate(async () => {
      const payload = { title: form.title, content: form.content, mood: form.mood, date: form.entryDate, requestId: editEntry ? undefined : requestId.current, expectedUpdatedAt: editEntry?.updatedAt }
      if (editEntry) {
        await api.put(`/journal/${editEntry.id}`, payload)
      } else {
        await api.post('/journal', payload)
      }
      sessionStorage.removeItem(draftKey)
      setFormSnapshot(JSON.stringify(form))
      setShowEditor(false)
      loadAll()
    })
  }

  const deleteEntry = (id: string) => mutate(async () => {
    if (!confirm('Delete this journal entry?')) return
    await api.delete(`/journal/${id}`); await loadAll()
  })

  const saveSettings = async () => {
    setSyncStatus(null)
    try {
      const { data } = await api.put('/journal/settings', settingsForm)
      setSettings(data)
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

  const filteredEntries = entries.filter(e => (e.title + ' ' + e.content + ' ' + (e.mood || '') + ' ' + (e.localDate || e.entryDate.slice(0, 10))).toLowerCase().includes(query.toLowerCase()))

  if (isLoading) {
    return (
      <div className="p-8 flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-4xl mx-auto">
      {error && <div role="alert" className="fixed top-4 left-1/2 -translate-x-1/2 z-[70] w-[min(90vw,40rem)] bg-card border border-destructive rounded p-3 text-sm">{error} <button className="underline" onClick={loadAll}>Retry loading</button></div>}
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

      <input aria-label="Search journal" placeholder="Search entries, dates or mood…" value={query} onChange={e => setQuery(e.target.value)} className="w-full border rounded bg-background p-3 mb-4" />
      {/* Editor Modal */}
      {showEditor && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => { if (!dirty || window.confirm("Discard unsaved changes?")) setShowEditor(false) }}>
          <div className="bg-card border rounded-lg p-6 w-full max-w-[640px] mx-4 max-h-[85vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
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
              <Button variant="outline" onClick={() => { if (!dirty || window.confirm("Discard unsaved changes?")) setShowEditor(false) }}>Cancel</Button>
              <Button disabled={busy || !form.title.trim()} onClick={saveEntry}>Save Entry</Button>
            </div>
          </div>
        </div>
      )}

      <p className="text-xs text-muted-foreground mt-4">Google Docs sync updates the marked LifeOS export section. Text outside that section is preserved; edits inside it are replaced on the next sync.</p>

      {/* Settings Modal */}
      {showSettings && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50" onClick={() => setShowSettings(false)}>
          <div className="bg-card border rounded-lg p-6 w-full max-w-[680px] mx-4 max-h-[88vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold flex items-center gap-2">
                <Cloud className="h-5 w-5 text-primary" />
                Google Docs Backup
              </h3>
              <Button variant="ghost" size="sm" onClick={() => setShowSettings(false)}><X className="h-4 w-4" /></Button>
            </div>

            {/* How-to guide */}
            <div className="bg-secondary/40 rounded-md p-4 mb-5 text-sm space-y-2">
              <p className="font-medium">Quick setup (~1 minute):</p>
              <ol className="list-decimal pl-5 space-y-1.5 text-muted-foreground">
                <li>Create a new <a href="https://docs.google.com" target="_blank" rel="noopener noreferrer" className="text-primary underline">Google Doc</a> — this is where your journal gets backed up.</li>
                <li>In the Doc, click <strong>Share</strong> → add this email with <strong>Editor</strong> access:
                  {settings?.serviceAccountEmail ? (
                    <code className="block mt-1 px-2 py-1.5 bg-background rounded text-primary text-xs select-all break-all">{settings.serviceAccountEmail}</code>
                  ) : (
                    <span className="block mt-1 text-amber-500 text-xs">(service account not configured on server)</span>
                  )}
                </li>
                <li>Copy the Doc's <strong>ID</strong> from its URL — the long string between <code>/d/</code> and <code>/edit</code> — and paste it below.</li>
              </ol>
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
              <Button variant="outline" onClick={testConnection} disabled={isTesting || !settings?.hasServiceAccount}>
                {isTesting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Cloud className="mr-2 h-4 w-4" />}
                Test Connection
              </Button>
              <Button onClick={saveSettings}>Save Settings</Button>
            </div>
            <p className="text-xs text-muted-foreground mt-3">
              Save first, then "Test Connection" to verify, and use "Sync Now" on the Journal page to push all entries into your doc.
            </p>
          </div>
        </div>
      )}

      {/* Entries */}
      <div className="space-y-4">
        {filteredEntries.length === 0 ? (
          <div className="bg-card border rounded-lg p-12 text-center">
            <BookOpen className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-muted-foreground">{query ? 'No entries match your search.' : 'No journal entries yet.'}</p>
            <p className="text-sm text-muted-foreground mt-1">Write your first entry to get started.</p>
          </div>
        ) : (
          filteredEntries.map(entry => (
            <div key={entry.id} className="bg-card border rounded-lg p-5">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-xs text-muted-foreground">
                    {new Date((entry.localDate || entry.entryDate.slice(0,10)) + 'T12:00:00').toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
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
