# LifeOS

Self-hosted personal operating system to become a better software engineer, stay disciplined, and grow spiritually.

## Architecture

- **Backend:** ASP.NET Core 9 (Clean Architecture)
- **Frontend:** React 19 + Vite + TypeScript + TailwindCSS + shadcn/ui
- **Database:** PostgreSQL 16
- **AI:** Google Gemini + Ollama (local fallback)
- **Infra:** Docker Compose, nginx, designed for Unraid

## Quick Start

```bash
# 1. Clone and enter directory
cd LifeOS

# 2. Copy environment file and edit with your secrets
cp .env.example .env

# 3. Start everything
docker compose up -d

# 4. Open http://localhost
```

## Development

### Backend
```bash
cd src
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH:$HOME/.dotnet/tools"
dotnet build
dotnet ef migrations add Name --project LifeOS.Infrastructure --startup-project LifeOS.Api
dotnet run --project LifeOS.Api
```

### Frontend
```bash
cd frontend
npm install
npm run dev
```

## Modules

- Dashboard ("What should I do today?")
- Jobs (Jab-match integration)
- Habits & XP
- AI Coach
- Coding (Phase 2)
- Bible (Phase 2)
- Portfolio (Phase 2)
- Calendar (Phase 2)
- Analytics (Phase 2)
- Notifications (Phase 2)

## License

MIT
