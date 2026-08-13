import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Pencil, ChefHat, Cloud, Settings, X, CheckCircle, AlertCircle, RefreshCw, Minus, Users } from 'lucide-react'

interface Ingredient {
  id?: string
  name: string
  quantity: number
  unit?: string
  notes?: string
}

interface Recipe {
  id: string
  name: string
  description?: string
  category?: string
  baseServings: number
  prepTime?: string
  cookTime?: string
  instructions: string
  ingredients: Ingredient[]
}

interface RecipeSettings {
  googleDocId?: string
  hasServiceAccount: boolean
  serviceAccountEmail?: string
  autoSync: boolean
  lastSyncAt?: string
}

const UNITS = ['', 'pcs', 'cup', 'tbsp', 'tsp', 'oz', 'lb', 'g', 'kg', 'ml', 'l', 'pinch', 'clove', 'can', 'package', 'slice', 'bunch']
const CATEGORIES = ['', 'Breakfast', 'Lunch', 'Dinner', 'Dessert', 'Snack', 'Drink', 'Side', 'Other']

// Format a decimal quantity with common cooking fractions
function formatQty(qty: number): string {
  if (qty <= 0) return '0'
  const rounded = Math.round(qty * 100) / 100
  const whole = Math.floor(rounded)
  const frac = rounded - whole

  const fracs: Array<[number, string]> = [
    [0.25, '¼'], [0.33, '⅓'], [0.5, '½'], [0.66, '⅔'], [0.67, '⅔'], [0.75, '¾'],
  ]

  let fracStr = ''
  for (const [val, sym] of fracs) {
    if (Math.abs(frac - val) < 0.02) { fracStr = sym; break }
  }

  if (fracStr) {
    return whole > 0 ? `${whole}${fracStr}` : fracStr
  }
  return rounded % 1 === 0 ? String(rounded) : String(rounded)
}

export default function RecipesPage() {
  const [recipes, setRecipes] = useState<Recipe[]>([])
  const [settings, setSettings] = useState<RecipeSettings | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const [servingsMap, setServingsMap] = useState<Record<string, number>>({})

  const [showEditor, setShowEditor] = useState(false)
  const [editRecipe, setEditRecipe] = useState<Recipe | null>(null)
  const [form, setForm] = useState({
    name: '', description: '', category: '', baseServings: 4, prepTime: '', cookTime: '', instructions: '',
  })
  const [ingredients, setIngredients] = useState<Ingredient[]>([])

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
      const [recipesRes, settingsRes] = await Promise.all([
        api.get('/recipes'),
        api.get('/recipes/settings'),
      ])
      setRecipes(recipesRes.data)
      setSettings(settingsRes.data)
      setSettingsForm(f => ({ ...f, googleDocId: settingsRes.data.googleDocId || '', autoSync: settingsRes.data.autoSync || false }))
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const getServings = (recipe: Recipe) => servingsMap[recipe.id] ?? recipe.baseServings

  const adjustServings = (recipe: Recipe, delta: number) => {
    const current = getServings(recipe)
    const next = Math.max(1, current + delta)
    setServingsMap({ ...servingsMap, [recipe.id]: next })
  }

  const scaledQty = (ing: Ingredient, recipe: Recipe) => {
    const target = getServings(recipe)
    return (ing.quantity * target) / recipe.baseServings
  }

  const openNew = () => {
    setEditRecipe(null)
    setForm({ name: '', description: '', category: '', baseServings: 4, prepTime: '', cookTime: '', instructions: '' })
    setIngredients([{ name: '', quantity: 1, unit: '' }])
    setShowEditor(true)
  }

  const openEdit = (recipe: Recipe) => {
    setEditRecipe(recipe)
    setForm({
      name: recipe.name,
      description: recipe.description || '',
      category: recipe.category || '',
      baseServings: recipe.baseServings,
      prepTime: recipe.prepTime || '',
      cookTime: recipe.cookTime || '',
      instructions: recipe.instructions,
    })
    setIngredients(recipe.ingredients.map(i => ({ name: i.name, quantity: i.quantity, unit: i.unit || '', notes: i.notes || '' })))
    setShowEditor(true)
  }

  const saveRecipe = async () => {
    if (!form.name.trim()) return
    const payload = {
      ...form,
      ingredients: ingredients.filter(i => i.name.trim()),
    }
    try {
      if (editRecipe) {
        await api.put(`/recipes/${editRecipe.id}`, payload)
      } else {
        await api.post('/recipes', payload)
      }
      setShowEditor(false)
      loadAll()
    } catch (err) {
      console.error(err)
      alert('Failed to save recipe')
    }
  }

  const deleteRecipe = async (id: string) => {
    if (!confirm('Delete this recipe?')) return
    try {
      await api.delete(`/recipes/${id}`)
      loadAll()
    } catch (err) {
      console.error(err)
    }
  }

  const addIngredientRow = () => setIngredients([...ingredients, { name: '', quantity: 1, unit: '' }])
  const removeIngredientRow = (idx: number) => setIngredients(ingredients.filter((_, i) => i !== idx))
  const updateIngredient = (idx: number, patch: Partial<Ingredient>) => {
    const next = [...ingredients]
    next[idx] = { ...next[idx], ...patch }
    setIngredients(next)
  }

  const saveSettings = async () => {
    setSyncStatus(null)
    try {
      const { data } = await api.put('/recipes/settings', settingsForm)
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
      const { data } = await api.post('/recipes/sync/test')
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
      const { data } = await api.post('/recipes/sync')
      setSyncStatus({ ok: true, msg: `Synced ${data.recipesSynced} recipes` })
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
    <div className="p-8 max-w-5xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <ChefHat className="h-6 w-6 text-primary" />
            Recipes
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {settings?.hasServiceAccount && settings?.googleDocId
              ? `Backing up to Google Docs${settings.lastSyncAt ? ` · last synced ${new Date(settings.lastSyncAt).toLocaleString()}` : ''}`
              : 'Your recipe book with serving calculator and Google Docs backup'}
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
            New Recipe
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
          <div className="bg-card border rounded-lg p-6 w-[720px] max-h-[88vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h3 className="text-lg font-semibold mb-4">{editRecipe ? 'Edit Recipe' : 'New Recipe'}</h3>
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <input type="text" placeholder="Recipe name *" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm" />
                <select value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} className="px-3 py-2 rounded-md border bg-background text-sm">
                  {CATEGORIES.map(c => <option key={c} value={c}>{c || 'Category...'}</option>)}
                </select>
              </div>
              <input type="text" placeholder="Short description (optional)" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} className="w-full px-3 py-2 rounded-md border bg-background text-sm" />
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="text-xs text-muted-foreground mb-1 block">Base servings</label>
                  <input type="number" min={1} value={form.baseServings} onChange={e => setForm({ ...form, baseServings: parseInt(e.target.value) || 1 })} className="w-full px-3 py-2 rounded-md border bg-background text-sm" />
                </div>
                <div>
                  <label className="text-xs text-muted-foreground mb-1 block">Prep time</label>
                  <input type="text" placeholder="15 min" value={form.prepTime} onChange={e => setForm({ ...form, prepTime: e.target.value })} className="w-full px-3 py-2 rounded-md border bg-background text-sm" />
                </div>
                <div>
                  <label className="text-xs text-muted-foreground mb-1 block">Cook time</label>
                  <input type="text" placeholder="30 min" value={form.cookTime} onChange={e => setForm({ ...form, cookTime: e.target.value })} className="w-full px-3 py-2 rounded-md border bg-background text-sm" />
                </div>
              </div>

              {/* Ingredient manager */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <label className="text-sm font-medium">Ingredients (for {form.baseServings} servings)</label>
                  <Button size="sm" variant="outline" onClick={addIngredientRow}>
                    <Plus className="mr-1 h-3 w-3" /> Add
                  </Button>
                </div>
                <div className="space-y-2">
                  {ingredients.map((ing, idx) => (
                    <div key={idx} className="flex gap-2 items-center">
                      <input
                        type="number" min={0} step="0.25"
                        value={ing.quantity}
                        onChange={e => updateIngredient(idx, { quantity: parseFloat(e.target.value) || 0 })}
                        className="w-20 px-2 py-2 rounded-md border bg-background text-sm"
                      />
                      <select value={ing.unit || ''} onChange={e => updateIngredient(idx, { unit: e.target.value })} className="w-28 px-2 py-2 rounded-md border bg-background text-sm">
                        {UNITS.map(u => <option key={u} value={u}>{u || 'unit'}</option>)}
                      </select>
                      <input
                        type="text" placeholder="Ingredient name"
                        value={ing.name}
                        onChange={e => updateIngredient(idx, { name: e.target.value })}
                        className="flex-1 px-3 py-2 rounded-md border bg-background text-sm"
                      />
                      <input
                        type="text" placeholder="Notes"
                        value={ing.notes || ''}
                        onChange={e => updateIngredient(idx, { notes: e.target.value })}
                        className="w-32 px-2 py-2 rounded-md border bg-background text-sm"
                      />
                      <button onClick={() => removeIngredientRow(idx)} className="text-destructive hover:text-destructive/80 shrink-0">
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>
              </div>

              <div>
                <label className="text-sm font-medium mb-1 block">Instructions</label>
                <textarea
                  placeholder="Step by step instructions..."
                  value={form.instructions}
                  onChange={e => setForm({ ...form, instructions: e.target.value })}
                  className="w-full px-3 py-2 rounded-md border bg-background text-sm min-h-[140px]"
                />
              </div>
            </div>
            <div className="flex gap-2 mt-4 justify-end">
              <Button variant="outline" onClick={() => setShowEditor(false)}>Cancel</Button>
              <Button onClick={saveRecipe}>Save Recipe</Button>
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

            <div className="bg-secondary/40 rounded-md p-4 mb-5 text-sm space-y-2">
              <p className="font-medium">Quick setup (~1 minute):</p>
              <ol className="list-decimal pl-5 space-y-1.5 text-muted-foreground">
                <li>Create a new <a href="https://docs.google.com" target="_blank" rel="noopener noreferrer" className="text-primary underline">Google Doc</a> — your recipe book backup lives there.</li>
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
          </div>
        </div>
      )}

      {/* Recipe cards */}
      <div className="space-y-5">
        {recipes.length === 0 ? (
          <div className="bg-card border rounded-lg p-12 text-center">
            <ChefHat className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <p className="text-muted-foreground">No recipes yet.</p>
            <p className="text-sm text-muted-foreground mt-1">Add your first recipe to get started.</p>
          </div>
        ) : (
          recipes.map(recipe => {
            const servings = getServings(recipe)
            return (
              <div key={recipe.id} className="bg-card border rounded-lg p-6">
                <div className="flex items-start justify-between mb-3">
                  <div>
                    <h3 className="text-lg font-semibold">{recipe.name}</h3>
                    <p className="text-sm text-muted-foreground">
                      {[recipe.category, recipe.prepTime && `Prep: ${recipe.prepTime}`, recipe.cookTime && `Cook: ${recipe.cookTime}`].filter(Boolean).join('  ·  ')}
                    </p>
                    {recipe.description && <p className="text-sm text-muted-foreground mt-1">{recipe.description}</p>}
                  </div>
                  <div className="flex gap-1 shrink-0">
                    <Button variant="ghost" size="sm" onClick={() => openEdit(recipe)}>
                      <Pencil className="h-4 w-4 text-muted-foreground" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => deleteRecipe(recipe.id)}>
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </div>

                {/* Serving scaler */}
                <div className="flex items-center gap-3 mb-4 bg-secondary/40 rounded-md px-3 py-2 w-fit">
                  <Users className="h-4 w-4 text-primary" />
                  <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={() => adjustServings(recipe, -1)} disabled={servings <= 1}>
                    <Minus className="h-3 w-3" />
                  </Button>
                  <span className="text-sm font-medium min-w-[90px] text-center">
                    {servings} serving{servings !== 1 ? 's' : ''}
                  </span>
                  <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={() => adjustServings(recipe, 1)}>
                    <Plus className="h-3 w-3" />
                  </Button>
                  {servings !== recipe.baseServings && (
                    <button onClick={() => setServingsMap({ ...servingsMap, [recipe.id]: recipe.baseServings })} className="text-xs text-primary hover:underline ml-1">
                      reset
                    </button>
                  )}
                </div>

                {/* Ingredients (scaled) */}
                {recipe.ingredients.length > 0 && (
                  <div className="mb-4">
                    <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">Ingredients</h4>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-1">
                      {recipe.ingredients.map((ing, i) => (
                        <div key={i} className="text-sm flex justify-between border-b border-border/40 pb-1">
                          <span>
                            {ing.name}
                            {ing.notes && <span className="text-muted-foreground text-xs"> ({ing.notes})</span>}
                          </span>
                          <span className="font-medium text-primary shrink-0 ml-3">
                            {formatQty(scaledQty(ing, recipe))} {ing.unit}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Instructions */}
                {recipe.instructions && (
                  <div>
                    <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">Instructions</h4>
                    <p className="text-sm whitespace-pre-line text-muted-foreground">{recipe.instructions}</p>
                  </div>
                )}
              </div>
            )
          })
        )}
      </div>
    </div>
  )
}
