import { Routes, Route, Navigate } from 'react-router-dom'
import AuthLayout from '@/modules/auth/AuthLayout'
import LoginPage from '@/modules/auth/LoginPage'
import RegisterPage from '@/modules/auth/RegisterPage'
import MainLayout from '@/modules/dashboard/MainLayout'
import DashboardPage from '@/modules/dashboard/DashboardPage'
import ProfilePage from '@/modules/profile/ProfilePage'
import EducationPage from '@/modules/education/EducationPage'
import ExperiencePage from '@/modules/experience/ExperiencePage'
import SkillsPage from '@/modules/skills/SkillsPage'
import JobsPage from '@/modules/jobs/JobsPage'
import ApplicationsPage from '@/modules/jobs/ApplicationsPage'
import CompanyNotesPage from '@/modules/jobs/CompanyNotesPage'
import HabitsPage from '@/modules/habits/HabitsPage'
import AiCoachPage from '@/modules/ai-coach/AiCoachPage'
import ResumeEditorPage from '@/modules/resume/ResumeEditorPage'
import DocumentsPage from '@/modules/documents/DocumentsPage'

function App() {
  return (
    <Routes>
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
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/education" element={<EducationPage />} />
          <Route path="/experience" element={<ExperiencePage />} />
          <Route path="/skills" element={<SkillsPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default App
