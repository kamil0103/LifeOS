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

function App() {
  return (
    <Routes>
      <Route element={<AuthLayout />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route element={<MainLayout />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/jobs" element={<div className="p-8">Jobs module coming soon...</div>} />
          <Route path="/habits" element={<div className="p-8">Habits module coming soon...</div>} />
          <Route path="/ai-coach" element={<div className="p-8">AI Coach module coming soon...</div>} />
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
