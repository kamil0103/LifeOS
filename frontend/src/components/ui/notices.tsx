import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
interface Notice { id: number; message: string; type: 'success' | 'error' | 'info' }
export function Notices() {
  const [items, setItems] = useState<Notice[]>([])
  useEffect(() => {
    const receive = (event: Event) => {
      const detail = (event as CustomEvent).detail
      const id = Date.now() + Math.random()
      setItems(items => [...items.filter(item => item.message !== detail.message), { ...detail, id }].slice(-3))
      window.setTimeout(() => setItems(items => items.filter(item => item.id !== id)), detail.type === 'error' ? 12000 : 6000)
    }
    window.addEventListener('lifeos:notice', receive)
    return () => window.removeEventListener('lifeos:notice', receive)
  }, [])
  return <div className="fixed bottom-4 right-4 z-[100] w-[calc(100%-2rem)] max-w-sm space-y-2" aria-live="polite">
    {items.map(item => <div key={item.id} role={item.type === 'error' ? 'alert' : 'status'} className={`rounded-xl border p-4 shadow-xl bg-slate-900 flex gap-3 ${item.type === 'error' ? 'border-red-400/40' : item.type === 'success' ? 'border-green-400/40' : 'border-blue-400/40'}`}>
      <p className="flex-1 text-sm text-white">{item.message}</p>
      <button aria-label="Dismiss message" onClick={() => setItems(items => items.filter(i => i.id !== item.id))}><X size={16} /></button>
    </div>)}
  </div>
}
