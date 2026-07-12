import { Routes, Route } from 'react-router-dom'

function App() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <Routes>
        <Route path="/" element={<div className="p-8">LifeOS Dashboard coming soon...</div>} />
      </Routes>
    </div>
  )
}

export default App
