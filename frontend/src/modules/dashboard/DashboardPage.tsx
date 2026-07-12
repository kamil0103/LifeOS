export default function DashboardPage() {
  return (
    <div className="p-8 space-y-8">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground mt-1">What should you do today?</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <h2 className="text-lg font-semibold">Today's Mission</h2>
          <p className="text-muted-foreground mt-2 text-sm">
            Your daily mission will appear here once the AI Coach is configured.
          </p>
        </div>

        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <h2 className="text-lg font-semibold">Streak & XP</h2>
          <p className="text-muted-foreground mt-2 text-sm">
            Track your progress as you complete habits and tasks.
          </p>
        </div>

        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <h2 className="text-lg font-semibold">Job Applications</h2>
          <p className="text-muted-foreground mt-2 text-sm">
            Application tracking will be available in the Jobs module.
          </p>
        </div>

        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <h2 className="text-lg font-semibold">Coding Progress</h2>
          <p className="text-muted-foreground mt-2 text-sm">
            Coding assignments and portfolio tracking coming soon.
          </p>
        </div>

        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <h2 className="text-lg font-semibold">Bible Reading</h2>
          <p className="text-muted-foreground mt-2 text-sm">
            Daily reading plan and prayer journal coming in Phase 2.
          </p>
        </div>

        <div className="bg-card border rounded-lg p-6 shadow-sm">
          <h2 className="text-lg font-semibold">AI Insights</h2>
          <p className="text-muted-foreground mt-2 text-sm">
            Personalized coaching insights will appear here.
          </p>
        </div>
      </div>
    </div>
  )
}
