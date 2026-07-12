# LifeOS — Agent Development Guide

## Project Overview
Self-hosted personal operating system to improve as a software engineer, stay disciplined, and grow spiritually. Built with Clean Architecture.

## Tech Stack
- **Backend:** ASP.NET Core 9, EF Core 9, PostgreSQL 16, QuestPDF
- **Frontend:** React 19, Vite, TypeScript, TailwindCSS, shadcn/ui
- **AI:** Google Gemini (primary) + Ollama (local fallback)
- **Infra:** Docker Compose, nginx reverse proxy, designed for Unraid

## Architecture Rules
1. **Dependency Rule:** Domain -> Application -> Infrastructure -> Api
2. No business logic in controllers. Controllers delegate to Application services.
3. All external concerns (DB, AI, PDF) implement interfaces defined in Application.
4. Use `UUID` (Guid) for all entity IDs.
5. Use `DateTimeOffset` (mapped to `TIMESTAMPTZ` in PostgreSQL).
6. Soft deletes only via `IsDeleted` flag where applicable; otherwise hard delete with cascades.

## Database
- PostgreSQL via EF Core.
- Migrations are code-first. Run `dotnet ef migrations add Name` in Infrastructure project.
- Seeding: `DataSeeder` runs on startup for Bible WEB data (Phase 2) and default admin user.

## API Conventions
- RESTful routes under `/api/`
- Return `ProblemDetails` for errors via custom middleware.
- DTOs in `Application/DTOs/`. Use `FluentValidation`.
- Controllers return `ActionResult<T>`.

## Frontend Conventions
- Dark mode first. All shadcn components configured with `dark` class on `<html>`.
- Feature-based folders under `src/modules/`.
- Shared UI in `src/components/ui/`.
- API client in `src/lib/api.ts` using axios with interceptors for JWT refresh.
- React Query (TanStack Query) for server state.
- Zustand for client state (auth, theme).

## AI Integration
- `IAiProvider` interface in Application.
- `GeminiProvider` and `OllamaProvider` in Infrastructure.
- Prompts are stored as const strings or loaded from embedded resources.
- Never auto-save AI-extracted data without user review (e.g., transcript courses).

## Commit Convention
Use conventional commits:
- `feat(module): description`
- `fix(module): description`
- `refactor(module): description`
- `test(module): description`
- `docs: description`

## Testing Gate
Every module PR/MR must include:
- Unit tests for Application services (xUnit + Moq)
- Integration tests for API endpoints (WebApplicationFactory)
- Frontend component tests where applicable (Vitest)
