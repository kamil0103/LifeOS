import { useAuthStore } from '@/hooks/useAuthStore'
import { canLeavePage } from '@/hooks/useUnsavedChanges'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { Menu, X, ChevronDown, LogOut, LayoutDashboard, Briefcase, Target, BrainCircuit, GraduationCap, Building2, Wrench, Code2, BookOpen, NotebookPen, ChefHat, Globe, FileText, FolderOpen, Calendar, TrendingUp, Bell, UserCircle, ClipboardList, StickyNote } from 'lucide-react'
import { useState } from 'react'
const groups = [
  { name: 'Today', items: [{ path: '/', label: 'Dashboard', icon: LayoutDashboard }, { path: '/calendar', label: 'Calendar', icon: Calendar }, { path: '/habits', label: 'Habits', icon: Target }, { path: '/ai-coach', label: 'AI Coach', icon: BrainCircuit }, { path: '/notifications', label: 'Notifications', icon: Bell }] },
  { name: 'Career', items: [{ path: '/jobs', label: 'Jobs', icon: Briefcase }, { path: '/jobs/applications', label: 'Applications', icon: ClipboardList }, { path: '/jobs/notes', label: 'Company Notes', icon: StickyNote }, { path: '/resume', label: 'Resume', icon: FileText }, { path: '/documents', label: 'Documents', icon: FolderOpen }, { path: '/experience', label: 'Experience', icon: Building2 }, { path: '/portfolio', label: 'Portfolio', icon: Globe }] },
  { name: 'Learning', items: [{ path: '/education', label: 'Education', icon: GraduationCap }, { path: '/skills', label: 'Skills', icon: Wrench }, { path: '/coding', label: 'Coding', icon: Code2 }] },
  { name: 'Personal', items: [{ path: '/bible', label: 'Bible', icon: BookOpen }, { path: '/journal', label: 'Journal', icon: NotebookPen }, { path: '/recipes', label: 'Recipes', icon: ChefHat }] },
  { name: 'Settings', items: [{ path: '/analytics', label: 'Analytics', icon: TrendingUp }, { path: '/profile', label: 'Profile', icon: UserCircle }] },
]
export default function MainLayout() {
  const user = useAuthStore(state => state.user)
  const logout = useAuthStore(state => state.logout)
  const navigate = useNavigate()
  const location = useLocation()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [closed, setClosed] = useState<string[]>([])
  const go = (path: string) => { if (canLeavePage()) { navigate(path); setMobileOpen(false) } }
  return <div className="min-h-screen flex bg-[#080c18]">
    {mobileOpen && <button aria-label="Close navigation" className="fixed inset-0 bg-black/60 z-30 md:hidden" onClick={() => setMobileOpen(false)} />}
    <aside aria-label="Main navigation" className={`${mobileOpen ? 'flex' : 'hidden'} md:flex fixed inset-y-0 left-0 z-40 w-64 flex-col bg-[#0a0e1a] border-r border-white/5`}>
      <div className="p-5 flex items-center justify-between"><div><h1 className="text-lg font-bold text-white">LifeOS</h1><p className="text-xs text-white/40">Your daily workspace</p></div><button className="md:hidden" aria-label="Close navigation" onClick={() => setMobileOpen(false)}><X size={20} /></button></div>
      <nav className="flex-1 overflow-y-auto p-3 space-y-3">
        {groups.map(group => <div key={group.name}>
          <button className="flex justify-between items-center w-full px-3 py-2 text-xs text-white/40 uppercase tracking-wider" aria-expanded={!closed.includes(group.name)} onClick={() => setClosed(old => old.includes(group.name) ? old.filter(x => x !== group.name) : [...old, group.name])}>{group.name}<ChevronDown size={14} className={closed.includes(group.name) ? '-rotate-90' : ''} /></button>
          {!closed.includes(group.name) && group.items.map(item => <button key={item.path} aria-current={location.pathname === item.path ? 'page' : undefined} onClick={() => go(item.path)} className={`sidebar-item w-full ${location.pathname === item.path ? 'sidebar-item-active' : 'text-white/60 hover:text-white'}`}><item.icon size={17} />{item.label}</button>)}
        </div>)}
      </nav>
      <div className="p-4 border-t border-white/5"><p className="text-sm text-white truncate">{user?.fullName || user?.username}</p><button className="sidebar-item w-full mt-2 text-red-400" onClick={() => { if (canLeavePage()) logout() }}><LogOut size={17} />Sign Out</button></div>
    </aside>
    <main className="flex-1 min-w-0 md:ml-64">
      <header className="md:hidden sticky top-0 z-20 flex items-center gap-3 bg-[#0a0e1a] border-b border-white/5 p-3"><button aria-label="Open navigation" onClick={() => setMobileOpen(true)}><Menu size={22} /></button><span className="font-semibold">LifeOS</span></header>
      <div className="p-3 sm:p-6"><Outlet /></div>
    </main>
  </div>
}
