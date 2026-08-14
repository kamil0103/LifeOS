import { useState, useEffect } from 'react'
import api from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Loader2, Plus, Trash2, Pencil, ChefHat, Cloud, Settings, X, CheckCircle, AlertCircle, RefreshCw, Minus, Users, ArrowUp, ArrowDown, Clock, Thermometer } from 'lucide-react'

interface Ingredient {
  id?: string
  name: string
  quantity: number
  unit?: string
  notes?: string
}

interface StepVariable {
  name: string
  value: number
  unit?: string
  scalingMode: 'none' | 'linear' | 'sqrt'
}

interface RecipeStep {
  id?: string
  stepNumber: number
  stepType: string
  text: string
  variables: StepVariable[]
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
  steps: RecipeStep[]
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

const STEP_TYPES = ['prep', 'mix', 'cook', 'bake', 'fry', 'boil', 'simmer', 'rest', 'chill', 'serve', 'note', 'other']

const VARIABLE_PRESETS: Record<string, { unit: string; scalingMode: 'none' | 'linear' | 'sqrt' }> = {
  cooking_time: { unit: 'min', scalingMode: 'sqrt' },
  rest_time: { unit: 'min', scalingMode: 'linear' },
  chill_time: { unit: 'min', scalingMode: 'linear' },
  temperature: { unit: '°F', scalingMode: 'linear' },
  preheat: { unit: '°F', scalingMode: 'none' },
  water_temp: { unit: '°F', scalingMode: 'linear' },
  pressure: { unit: 'psi', scalingMode: 'none' },
  custom: { unit: '', scalingMode: 'none' },
}

const SCALING_MODES = [
  { value: 'none', label: 'fixed' },
  { value: 'linear', label: 'scales × servings' },
  { value: 'sqrt', label: 'scales gently (√)' },
]

// Format a decimal quantity with common cooking fractions
function formatQty(qty: number): string {
  if (qty <= 0) return '0'
  const rounded = Math.round(qty * 100) / 100
  const whole = Math.floor(rounded)
  const frac = rounded - whole
  const fracs: Array<[number, string]> = [[0.25, '¼'], [0.33, '⅓'], [0.5, '½'], [0.66, '⅔'], [0.67, '⅔'], [0.75, '¾']]
  let fracStr = ''
  for (const [val, sym] of fracs) {
    if (Math.abs(frac - val) < 0.02) { fracStr = sym; break }
  }
  if (fracStr) return whole > 0 ? `${whole}${fracStr}` : fracStr
  return rounded % 1 === 0 ? String(rounded) : String(rounded)
}

function scaleVariable(v: StepVariable, ratio: number): number {
  if (v.scalingMode === 'linear') return v.value * ratio
  if (v.scalingMode === 'sqrt') return v.value * Math.sqrt(ratio)
  return v.value
}

function formatVariable(v: StepVariable, ratio: number): string {
  const scaled = scaleVariable(v, ratio)
  const unit = v.unit || ''
  if (unit === 'min') return `${Math.round(scaled)} min`
  if (unit === '°F') return `${Math.round(scaled / 5) * 5}°F`
  if (unit === '°C') return `${Math.round(scaled)}°C`
  const rounded = Math.round(scaled * 10) / 10
  return `${rounded}${unit ? ' ' + unit : ''}`
}

function variableLabel(name: string): string {
  return name.replace(/_/g, ' ')
}

export default function RecipesPage() {
  const [recipes, setRecipes] = useState<Recipe[]>([])
  const [settings, setSettings] = useState<RecipeSettings | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const [servingsMap, setServingsMap] = useState<Record<string, number>>({})

  const [showEditor, setShowEditor] = useState(false)
  const [editRecipe, setEditRecipe] = useState<Recipe | null>(null)
  const [form, setForm] = useState({
    name: '', description: '', category: '', baseServings: 4, prepTime: '', cookTime: '',
  })
  const [ingredients, setIngredients] = useState<Ingredient[]>([])
  const [steps, setSteps] = useState<RecipeStep[]>([])

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

  const servingRatio = (recipe: Recipe) => getServings(recipe) / recipe.baseServings

  const openNew = () => {
    setEditRecipe(null)
    setForm({ name: '', description: '', category: '', baseServings: 4, prepTime: '', cookTime: '' })
    setIngredients([{ name: '', quantity: 1, unit: '' }])
    setSteps([{ stepNumber: 1, stepType: 'prep', text: '', variables: [] }])
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
    })
    setIngredients(recipe.ingredients.map(i => ({ name: i.name, quantity: i.quantity, unit: i.unit || '', notes: i.notes || '' })))
    if (recipe.steps && recipe.steps.length > 0) {
      setSteps(recipe.steps.map(s => ({
        stepNumber: s.stepNumber,
        stepType: s.stepType,
        text: s.text,
        variables: (s.variables || []).map(v => ({ ...v })),
      })))
    } else {
      // Legacy: wrap the old text block into one note step so nothing is lost
      setSteps(recipe.instructions
        ? [{ stepNumber: 1, stepType: 'note', text: recipe.instructions, variables: [] }]
        : [{ stepNumber: 1, stepType: 'prep', text: '', variables: [] }])
    }
    setShowEditor(true)
  }

  const saveRecipe = async () => {
    if (!form.name.trim()) return
    const payload = {
      ...form,
      instructions: '', // legacy field; steps are the source of truth now
      ingredients: ingredients.filter(i => i.name.trim()),
      steps: steps
        .filter(s => s.text.trim() || s.variables.length > 0)
        .map((s, i) => ({ ...s, stepNumber: i + 1 })),
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

  // ===== ingredient rows =====
  const addIngredientRow = () => setIngredients([...ingredients, { name: '', quantity: 1, unit: '' }])
  const removeIngredientRow = (idx: number) => setIngredients(ingredients.filter((_, i) => i !== idx))
  const updateIngredient = (idx: number, patch: Partial<Ingredient>) => {
    const next = [...ingredients]
    next[idx] = { ...next[idx], ...patch }
    setIngredients(next)
  }

  // ===== step rows =====
  const addStep = () => setSteps([...steps, { stepNumber: steps.length + 1, stepType: 'prep', text: '', variables: [] }])
  const removeStep = (idx: number) => setSteps(steps.filter((_, i) => i !== idx).map((s, i) => ({ ...s, stepNumber: i + 1 })))
  const updateStep = (idx: number, patch: Partial<RecipeStep>) => {
    const next = [...steps]
    next[idx] = { ...next[idx], ...patch }
    setSteps(next)
  }
  const moveStep = (idx: number, dir: -1 | 1) => {
    const target = idx + dir
    if (target < 0 || target >= steps.length) return
    const next = [...steps]
    ;[next[idx], next[target]] = [next[target], next[idx]]
    setSteps(next.map((s, i) => ({ ...s, stepNumber: i + 1 })))
  }

  // ===== step variables =====
  const addVariable = (stepIdx: number) => {
    const next = [...steps]
    next[stepIdx].variables.push({ name: 'cooking_time', value: 30, unit: 'min', scalingMode: 'sqrt' })
    setSteps(next)
  }
  const removeVariable = (stepIdx: number, varIdx: number) => {
    const next = [...steps]
    next[stepIdx].variables = next[stepIdx].variables.filter((_, i) => i !== varIdx)
    setSteps(next)
  }
  const updateVariable = (stepIdx: number, varIdx: number, patch: Partial<StepVariable>) => {
    const next = [...steps]
    const vars = [...next[stepIdx].variables]
    const updated = { ...vars[varIdx], ...patch }
    // apply preset defaults when name changes
    if (patch.name && VARIABLE_PRESETS[patch.name]) {
      const preset = VARIABLE_PRESETS[patch.name]
      updated.unit = preset.unit
      updated.scalingMode = preset.scalingMode
    }
    vars[varIdx] = updated
    next[stepIdx].variables = vars
    setSteps(next)
  }

  // ===== settings / sync =====
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
          <div className="bg-card border rounded-lg p-6 w-[780px] max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <h3 className="text-lg font-semibold mb-4">{editRecipe ? 'Edit Recipe' : 'New Recipe'}</h3>
            <div className="space-y-4">
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
                      <input type="number" min={0} step="0.25" value={ing.quantity} onChange={e => updateIngredient(idx, { quantity: parseFloat(e.target.value) || 0 })} className="w-20 px-2 py-2 rounded-md border bg-background text-sm" />
                      <select value={ing.unit || ''} onChange={e => updateIngredient(idx, { unit: e.target.value })} className="w-28 px-2 py-2 rounded-md border bg-background text-sm">
                        {UNITS.map(u => <option key={u} value={u}>{u || 'unit'}</option>)}
                      </select>
                      <input type="text" placeholder="Ingredient name" value={ing.name} onChange={e => updateIngredient(idx, { name: e.target.value })} className="flex-1 px-3 py-2 rounded-md border bg-background text-sm" />
                      <input type="text" placeholder="Notes" value={ing.notes || ''} onChange={e => updateIngredient(idx, { notes: e.target.value })} className="w-32 px-2 py-2 rounded-md border bg-background text-sm" />
                      <button onClick={() => removeIngredientRow(idx)} className="text-destructive hover:text-destructive/80 shrink-0">
                        <X className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>
              </div>

              {/* Modular steps builder */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <label className="text-sm font-medium">Instructions (modular steps)</label>
                  <Button size="sm" variant="outline" onClick={addStep}>
                    <Plus className="mr-1 h-3 w-3" /> Add Step
                  </Button>
                </div>
                <div className="space-y-3">
                  {steps.map((step, stepIdx) => (
                    <div key={stepIdx} className="border border-border/60 rounded-md p-3 bg-secondary/20">
                      <div className="flex items-center gap-2 mb-2">
                        <span className="text-xs font-bold text-muted-foreground w-5">{stepIdx + 1}.</span>
                        <select value={step.stepType} onChange={e => updateStep(stepIdx, { stepType: e.target.value })} className="px-2 py-1.5 rounded-md border bg-background text-sm">
                          {STEP_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
                        </select>
                        <input
                          type="text"
                          placeholder="What to do in this step..."
                          value={step.text}
                          onChange={e => updateStep(stepIdx, { text: e.target.value })}
                          className="flex-1 px-3 py-1.5 rounded-md border bg-background text-sm"
                        />
                        <button onClick={() => moveStep(stepIdx, -1)} disabled={stepIdx === 0} className="text-muted-foreground hover:text-foreground disabled:opacity-30">
                          <ArrowUp className="h-4 w-4" />
                        </button>
                        <button onClick={() => moveStep(stepIdx, 1)} disabled={stepIdx === steps.length - 1} className="text-muted-foreground hover:text-foreground disabled:opacity-30">
                          <ArrowDown className="h-4 w-4" />
                        </button>
                        <button onClick={() => removeStep(stepIdx)} className="text-destructive hover:text-destructive/80">
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>

                      {/* variables */}
                      {step.variables.map((v, varIdx) => (
                        <div key={varIdx} className="flex gap-2 items-center ml-7 mb-1.5">
                          <select value={v.name} onChange={e => updateVariable(stepIdx, varIdx, { name: e.target.value })} className="w-36 px-2 py-1.5 rounded-md border bg-background text-xs">
                            {Object.keys(VARIABLE_PRESETS).map(n => <option key={n} value={n}>{variableLabel(n)}</option>)}
                          </select>
                          <input
                            type="number" min={0} step="any"
                            value={v.value}
                            onChange={e => updateVariable(stepIdx, varIdx, { value: parseFloat(e.target.value) || 0 })}
                            className="w-24 px-2 py-1.5 rounded-md border bg-background text-xs"
                          />
                          <input
                            type="text"
                            value={v.unit || ''}
                            onChange={e => updateVariable(stepIdx, varIdx, { unit: e.target.value })}
                            placeholder="unit"
                            className="w-16 px-2 py-1.5 rounded-md border bg-background text-xs"
                          />
                          <select value={v.scalingMode} onChange={e => updateVariable(stepIdx, varIdx, { scalingMode: e.target.value as 'none' | 'linear' | 'sqrt' })} className="px-2 py-1.5 rounded-md border bg-background text-xs">
                            {SCALING_MODES.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
                          </select>
                          <button onClick={() => removeVariable(stepIdx, varIdx)} className="text-destructive hover:text-destructive/80">
                            <X className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      ))}
                      <button onClick={() => addVariable(stepIdx)} className="ml-7 text-xs text-primary hover:underline flex items-center gap-1">
                        <Plus className="h-3 w-3" /> Add variable (time, temp...)
                      </button>
                    </div>
                  ))}
                </div>
                <p className="text-xs text-muted-foreground mt-2">
                  Variables like cooking time and temperature adapt when you change servings on the recipe card. The original recipe values never change.
                </p>
              </div>
            </div>
            <div className="flex gap-2 mt-5 justify-end">
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
            const ratio = servingRatio(recipe)
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

                {/* Serving scaler — view-only, never modifies the saved recipe */}
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

                {/* Modular steps (variables scaled live; base values untouched) */}
                {recipe.steps && recipe.steps.length > 0 ? (
                  <div>
                    <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">Instructions</h4>
                    <div className="space-y-2">
                      {recipe.steps.map((step, i) => (
                        <div key={i} className="flex gap-3 text-sm">
                          <span className="text-muted-foreground font-medium shrink-0 w-5">{i + 1}.</span>
                          <div className="flex-1">
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className="text-[10px] uppercase tracking-wider px-1.5 py-0.5 rounded bg-primary/10 text-primary">{step.stepType}</span>
                              <span className="text-muted-foreground">{step.text}</span>
                            </div>
                            {step.variables && step.variables.length > 0 && (
                              <div className="flex flex-wrap gap-2 mt-1">
                                {step.variables.map((v, vi) => (
                                  <span key={vi} className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded bg-secondary text-foreground">
                                    {(v.unit === 'min' ? <Clock className="h-3 w-3 text-primary" /> : <Thermometer className="h-3 w-3 text-orange-400" />)}
                                    {variableLabel(v.name)}: <strong>{formatVariable(v, ratio)}</strong>
                                  </span>
                                ))}
                              </div>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                ) : recipe.instructions ? (
                  <div>
                    <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">Instructions</h4>
                    <p className="text-sm whitespace-pre-line text-muted-foreground">{recipe.instructions}</p>
                  </div>
                ) : null}
              </div>
            )
          })
        )}
      </div>
    </div>
  )
}
