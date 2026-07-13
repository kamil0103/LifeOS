import { useAuthStore } from '@/hooks/useAuthStore'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import {
  LayoutDashboard,
  Briefcase,
  Target,
  BrainCircuit,
  GraduationCap,
  Building2,
  Wrench,
  UserCircle,
  LogOut,
  User,
  ChevronDown,
  ClipboardList,
  StickyNote,
  FileText,
  FolderOpen,
  Code2,
  BookOpen,
  Globe,
  Calendar as CalendarIcon,
  TrendingUp,
  Bell,
} from 'lucide-react'
import { useState } from 'react'

const navItems = [
  { path: '/', label: 'Dashboard', icon: LayoutDashboard },
  {
    label: 'Jobs',
    icon: Briefcase,
    children: [
      { path: '/jobs', label: 'Discover', icon: Briefcase },
      { path: '/jobs/applications', label: 'Applications', icon: ClipboardList },
      { path: '/jobs/notes', label: 'Company Notes', icon: StickyNote },
    ]
  },
  { path: '/habits', label: 'Habits', icon: Target },
  { path: '/ai-coach', label: 'AI Coach', icon: BrainCircuit },
  { path: '/education', label: 'Education', icon: GraduationCap },
  { path: '/experience', label: 'Experience', icon: Building2 },
  { path: '/skills', label: 'Skills', icon: Wrench },
  { path: '/coding', label: 'Coding', icon: Code2 },
  { path: '/bible', label: 'Bible', icon: BookOpen },
  { path: '/portfolio', label: 'Portfolio', icon: Globe },
  { path: '/resume', label: 'Resume', icon: FileText },
  { path: '/documents', label: 'Documents', icon: FolderOpen },
  { path: '/calendar', label: 'Calendar', icon: CalendarIcon },
  { path: '/analytics', label: 'Analytics', icon: TrendingUp },
  { path: '/notifications', label: 'Notifications', icon: Bell },
  { path: '/profile', label: 'Profile', icon: UserCircle },
]

export default function MainLayout() {
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const navigate = useNavigate()
  const location = useLocation()
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>({ Jobs: true })

  const toggleGroup = (label: string) => {
    setExpandedGroups(prev => ({ ...prev, [label]: !prev[label] }))
  }

  return (
    <div className="min-h-screen flex">
      {/* Sidebar */}
      <aside className="w-64 flex flex-col relative">
        {/* Sidebar gradient background */}
        <div className="absolute inset-0 bg-gradient-to-b from-[#0a0e1a] via-[#0c1020] to-[#0a0e1a]" />
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjAiIGhlaWdodD0iMjAiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PGNpcmNsZSBjeD0iMSIgY3k9IjEiIHI9IjEiIGZpbGw9InJnYmEoMjU1LDI1NSwyNTUsMC4wMykiLz48L3N2Zz4=')] opacity-50" />
        
        <div className="relative z-10 flex flex-col h-full">
          {/* Logo */}
          <div className="p-5">
            <div className="flex items-center gap-3">
              <div className="h-9 w-9 rounded-xl bg-gradient-to-br from-blue-500/20 to-purple-500/20 border border-white/10 flex items-center justify-center">
                <LayoutDashboard className="h-5 w-5 text-blue-400" />
              </div>
              <div>
                <h1 className="text-lg font-bold tracking-tight text-white">LifeOS</h1>
                <p className="text-[10px] text-white/40 uppercase tracking-widest">Operating System</p>
              </div>
            </div>
          </div>

          {/* Nav */}
          <nav className="flex-1 px-3 py-2 space-y-0.5 overflow-y-auto">
            {navItems.map((item) => {
              if ('children' in item && item.children) {
                const isExpanded = expandedGroups[item.label] ?? false
                const isActive = item.children.some(c => c.path === location.pathname)
                return (
                  <div key={item.label}>
                    <button
                      onClick={() => toggleGroup(item.label)}
                      className={`sidebar-item w-full justify-between ${isActive ? 'sidebar-item-active' : 'text-white/60 hover:text-white'}`}
                    >
                      <div className="flex items-center gap-3">
                        <item.icon className="h-[18px] w-[18px]" />
                        {item.label}
                      </div>
                      <ChevronDown className={`h-4 w-4 transition-transform duration-200 ${isExpanded ? 'rotate-180' : ''}`} />
                    </button>
                    {isExpanded && (
                      <div className="ml-4 mt-0.5 space-y-0.5 border-l border-white/5 pl-3">
                        {item.children.map((child) => {
                          const ChildIcon = child.icon
                          return (
                            <button
                              key={child.path}
                              onClick={() => navigate(child.path)}
                              className={`sidebar-item w-full ${location.pathname === child.path ? 'sidebar-item-active' : 'text-white/50 hover:text-white'}`}
                            >
                              <ChildIcon className="h-[16px] w-[16px]" />
                              {child.label}
                            </button>
                          )
                        })}
                      </div>
                    )}
                  </div>
                )
              }

              const Icon = item.icon
              const isActive = location.pathname === item.path
              return (
                <button
                  key={item.path}
                  onClick={() => navigate(item.path!)}
                  className={`sidebar-item w-full ${isActive ? 'sidebar-item-active' : 'text-white/60 hover:text-white'}`}
                >
                  <Icon className="h-[18px] w-[18px]" />
                  {item.label}
                </button>
              )
            })}
          </nav>

          {/* User */}
          <div className="p-3 mt-auto">
            <div className="glass-card p-3 mb-2">
              <div className="flex items-center gap-3">
                <div className="h-9 w-9 rounded-full bg-gradient-to-br from-blue-500/30 to-purple-500/30 border border-white/10 flex items-center justify-center">
                  <User className="h-4 w-4 text-blue-300" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-white truncate">{user?.username}</p>
                  <p className="text-xs text-white/40 truncate">{user?.email}</p>
                </div>
              </div>
            </div>
            <button
              onClick={logout}
              className="sidebar-item w-full text-red-400/80 hover:text-red-400 hover:bg-red-500/5"
            >
              <LogOut className="h-[18px] w-[18px]" />
              Sign Out
            </button>
          </div>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-auto relative">
        <div className="absolute inset-0 bg-gradient-to-br from-[#080c18] via-[#0a0e1a] to-[#0c1024]" />
        <div className="relative z-10 p-6">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
