import { useState } from 'react'
import { Button } from '@/components/ui/button'
// Show complete changed sections as plain text; matching entries are never inferred by position.
export function ResumeComparison({ before, after, beforeLabel = 'Current draft', afterLabel = 'Selected version' }: { before: Record<string, unknown>; after: Record<string, unknown>; beforeLabel?: string; afterLabel?: string }) {
  const keys = Array.from(new Set([...Object.keys(before), ...Object.keys(after)]))
  const changed = keys.filter(key => JSON.stringify(before[key]) !== JSON.stringify(after[key]))
  const [showAll, setShowAll] = useState(false)
  return <section className="space-y-3"><p className="text-sm">{changed.length} sections or settings differ. Arrays are shown in full so additions, removals and order remain visible.</p><Button type="button" variant="outline" size="sm" onClick={() => setShowAll(!showAll)}>{showAll ? 'Show changed sections' : 'Show all sections'}</Button>{(showAll ? keys : changed).map(key => <details key={key} open={changed.includes(key)} className="border rounded p-3"><summary className="font-medium cursor-pointer">{key}{changed.includes(key) ? ' · Changed' : ' · Unchanged'}</summary><div className="grid sm:grid-cols-2 gap-3 mt-2">{[[beforeLabel, before[key]], [afterLabel, after[key]]].map(([label, value], i) => <div key={i}><h4 className="font-medium text-sm">{String(label)}</h4><pre className="whitespace-pre-wrap break-words text-xs max-h-80 overflow-auto bg-secondary/30 p-2 rounded">{typeof value === 'string' ? value : JSON.stringify(value ?? null, null, 2)}</pre></div>)}</div></details>)}</section>
}
