import { useEffect, useState } from 'react'
import api from '@/lib/api'
import CoachConversation from './CoachConversation'
import MissionWorkflow from './MissionWorkflow'
interface Insight { id: string; messageType: string; content: string }
export default function AiCoachPage() {
  const [messages, setMessages] = useState<Insight[]>([]), [error, setError] = useState('')
  const load = async () => { try { setMessages((await api.get<Insight[]>('/aicoach/messages')).data); setError('') } catch { setError('Could not load recent insights.') } }
  useEffect(() => { load(); window.addEventListener('coach-mission-updated', load); return () => window.removeEventListener('coach-mission-updated', load) }, [])
  return <div className="p-4 sm:p-8 max-w-4xl mx-auto"><header className="mb-8"><h1 className="text-3xl font-bold">AI Coach</h1><p className="text-muted-foreground mt-1">Your personal mentor for growth</p></header><MissionWorkflow />{error && <p role="alert">{error} <button className="underline" onClick={load}>Retry insights</button></p>}{messages.length > 0 && <section className="mb-8 space-y-3"><h2 className="font-semibold text-lg">Recent insights</h2>{messages.map(message => <article className="border rounded-lg p-4" key={message.id}><p className="capitalize text-sm text-muted-foreground">{message.messageType}</p><p className="text-sm whitespace-pre-wrap">{message.content}</p></article>)}</section>}<CoachConversation /></div>
}
