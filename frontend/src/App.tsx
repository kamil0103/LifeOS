import { Suspense, lazy } from 'react'
import { Notices } from '@/components/ui/notices'
import { Routes, Route, Navigate } from 'react-router-dom'
import AuthLayout from '@/modules/auth/AuthLayout'
const LoginPage = lazy(() => import('@/modules/auth/LoginPage'))
const RegisterPage = lazy(() => import('@/modules/auth/RegisterPage'))
import MainLayout from '@/modules/dashboard/MainLayout'
const DashboardPage = lazy(() => import('@/modules/dashboard/DashboardPage'))
const ProfilePage = lazy(() => import('@/modules/profile/ProfilePage'))
const EducationPage = lazy(() => import('@/modules/education/EducationPage'))
const ExperiencePage = lazy(() => import('@/modules/experience/ExperiencePage'))
const SkillsPage = lazy(() => import('@/modules/skills/SkillsPage'))
const JobsPage = lazy(() => import('@/modules/jobs/JobsPage'))
const ApplicationsPage = lazy(() => import('@/modules/jobs/ApplicationsPage'))
const CompanyNotesPage = lazy(() => import('@/modules/jobs/CompanyNotesPage'))
const HabitsPage = lazy(() => import('@/modules/habits/HabitsPage'))
const AiCoachPage = lazy(() => import('@/modules/ai-coach/AiCoachPage'))
const ResumeEditorPage = lazy(() => import('@/modules/resume/ResumeEditorPage'))
const DocumentsPage = lazy(() => import('@/modules/documents/DocumentsPage'))
const CodingTrackerPage = lazy(() => import('@/modules/coding/CodingTrackerPage'))
const BiblePage = lazy(() => import('@/modules/bible/BiblePage'))
const PortfolioPage = lazy(() => import('@/modules/portfolio/PortfolioPage'))
const CalendarPage = lazy(() => import('@/modules/calendar/CalendarPage'))
const AnalyticsPage = lazy(() => import('@/modules/analytics/AnalyticsPage'))
const NotificationsPage = lazy(() => import('@/modules/notifications/NotificationsPage'))
const JournalPage = lazy(() => import('@/modules/journal/JournalPage'))
const RecipesPage = lazy(() => import('@/modules/recipes/RecipesPage'))

function App() {
  return (
    <><Notices /><Suspense fallback={<div className="p-8 text-muted-foreground" role="status">Loading…</div>}><Routes>
      <Route element={<AuthLayout />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route element={<MainLayout />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/jobs" element={<JobsPage />} />
          <Route path="/jobs/applications" element={<ApplicationsPage />} />
          <Route path="/jobs/notes" element={<CompanyNotesPage />} />
          <Route path="/habits" element={<HabitsPage />} />
          <Route path="/ai-coach" element={<AiCoachPage />} />
          <Route path="/resume" element={<ResumeEditorPage />} />
          <Route path="/documents" element={<DocumentsPage />} />
          <Route path="/coding" element={<CodingTrackerPage />} />
          <Route path="/bible" element={<BiblePage />} />
          <Route path="/journal" element={<JournalPage />} />
          <Route path="/recipes" element={<RecipesPage />} />
          <Route path="/portfolio" element={<PortfolioPage />} />
          <Route path="/calendar" element={<CalendarPage />} />
          <Route path="/analytics" element={<AnalyticsPage />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/education" element={<EducationPage />} />
          <Route path="/experience" element={<ExperiencePage />} />
          <Route path="/skills" element={<SkillsPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes></Suspense></>
  )
}

export default App
