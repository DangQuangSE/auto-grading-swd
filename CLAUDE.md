# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

### Backend (.NET 8)
```bash
# Build entire solution
dotnet build be/AutoGrading.sln

# Build specific service
dotnet build be/src/Services/Grading/AutoGrading.Grading.Api/AutoGrading.Grading.Api.csproj

# Run tests (3 test projects)
dotnet test be/AutoGrading.sln

# Run specific test project
dotnet test be/src/BuildingBlocks/AutoGrading.Common.Tests/AutoGrading.Common.Tests.csproj
dotnet test be/src/Services/Grading/AutoGrading.Grading.Api.Tests/AutoGrading.Grading.Api.Tests.csproj
dotnet test be/src/Services/Catalog/AutoGrading.Catalog.Api.Tests/AutoGrading.Catalog.Api.Tests.csproj

# EF Core migration (run from service dir)
cd be/src/Services/Submission/AutoGrading.Submission.Api
dotnet ef migrations add <MigrationName>
```

### Frontend (React + Vite)
```bash
# user-web (student-facing)
cd fe/user-web && npm install && npm run dev    # dev server on :5173
cd fe/user-web && npm run build                 # production build
cd fe/user-web && npm run lint                  # eslint
cd fe/user-web && npm run test                  # vitest

# admin-web (lecturer/admin-facing)
cd fe/admin-web && npm install && npm run dev   # dev server on :5174
cd fe/admin-web && npm run build                # production build
cd fe/admin-web && npm run lint                 # eslint
cd fe/admin-web && npm run test                 # vitest

# Type check
cd fe/user-web && npx tsc --noEmit
cd fe/admin-web && npx tsc --noEmit
```

### Docker
```bash
# Full stack (11 services)
docker compose up -d --build

# Single service rebuild
docker compose up -d --build grading-api

# View logs
docker compose logs -f grading-api
```

## Architecture Overview

**Microservices pattern** — each service owns its own SQL Server database ("database per service"):

| Service | Port | Database | Responsibility |
|---------|------|----------|----------------|
| Identity | :5001 | AutoGrading.Identity | User accounts, JWT auth, Google OAuth |
| Catalog | :5002 | AutoGrading.Catalog | Subjects, assignments, rubrics, criteria |
| Submission | :5003 | AutoGrading.Submission | Student file uploads, artifact extraction |
| Grading | :5004 | AutoGrading.Grading | AI grading runs, final grades |
| Notification | :5005 | AutoGrading.Notification | User notifications |
| Gateway | :5500 | — | YARP reverse proxy, auth policy enforcement |
| user-web | :5173 | — | Student-facing React app |
| admin-web | :5174 | — | Lecturer/admin React app |

### Inter-service Communication

Services **never call each other via HTTP** (except Grading→Catalog/Submission via service JWT). Two patterns:

1. **RabbitMQ events** (`IEventBus`): topic exchange `autograding.events`, queue pattern `{ServiceName}.{EventName}`

| Event | Publisher | Consumers |
|-------|-----------|-----------|
| `RubricConfirmed` | Catalog | Grading (upserts local criteria copy) |
| `ArtifactsExtracted` | Submission | Grading (enqueues AiGradingJob) |
| `SubmissionUploaded` | Submission | Grading, Identity |
| `GradePublished` | Grading | Identity, Notification |
| `UserRegistered` | Identity | Notification |
| `ClassLecturerAssigned` | Catalog | Identity |
| `RubricParsed` | Catalog | Notification |
| `AiGradingCompleted` | Grading | Notification |

2. **HTTP via `ServiceAuthHandler`**: Grading calls Catalog/Submission APIs to fetch criteria and submission content using a service-to-service JWT (different from user JWT). Registered in DI via `IClientFactory<T>`.

### Key Background Jobs (Hangfire)

- `ExtractionJob` (Submission): Parse uploaded DOCX → extract text + embedded images → publish `ArtifactsExtracted`
- `AiGradingJob` (Grading): Fetch rubric criteria + submission artifacts → call OpenCode AI → save `AiCriterionScore` per criterion → publish `AiGradingCompleted`
- `RubricParsingJob` (Catalog): Parse uploaded rubric DOCX → extract criteria via AI → save to DB

### AI Grading Pipeline

```
Student uploads DOCX → Submission service (ExtractionJob)
    → ArtifactsExtracted event → Grading service (AiGradingJob)
        → calls OpenCode Zen API (mimo-v2.5-free, reasoning model)
        → saves AiCriterionScore per rubric criterion
        → AiGradingCompleted event

Lecturer reviews in admin-web (/review) → confirms scores → PublishGrade → FinalGrade
```

AI provider: OpenCode Zen (`https://opencode.ai/zen/v1`), model `mimo-v2.5-free` (vision-capable, reasoning-enabled). Config in `OpenCodeOptions` (BaseUrl, MaxCompletionTokens=16000, EnableVision=true).

### Auth Flow

- Identity service issues JWT with user ID, email, role (`student`/`lecturer`/`admin`)
- Gateway validates JWT and forwards claims to backend services
- Service-to-service calls use a separate JWT with service identity (via `ServiceAuthHandler`)
- `.env` contains `JWT_SIGNING_KEY` shared across all services

### Object Storage (MinIO)

Files (rubric DOCX, student submissions) stored in MinIO. DB stores only `ObjectKey` (path). Bucket: `autograding`. Key patterns: `rubrics/{guid}-{filename}`, `submissions/{guid}-{filename}`.

## Key Patterns

- **Minimal APIs**: each service has one `*Endpoints.cs` file defining all routes with `MapGet`/`MapPost`/etc — no controllers
- **Scoped DbContext**: every request gets its own `DbContext` instance — never inject as singleton
- **Event-driven cache**: Identity subscribes to Catalog events to maintain local denormalized caches (e.g. `ClassLecturerCache`)
- **Stub fallback**: when OpenCode API key is missing or AI fails, grading returns deterministic 80% scores as stub — allows pipeline testing without credentials

## Conventions

- Each service has its own `appsettings.json` with `ConnectionStrings`, `Jwt`, `RabbitMq`, `Minio`, and service-specific config
- `.env` (gitignored) holds real secrets; `.env.example` is committed with placeholder values
- EF Core migrations run automatically on startup via `app.MigrateDatabase<TContext>()` — snapshot file must match latest migration
- Frontend uses TanStack Query for server state, React Router for routing
- All API responses follow `{ items, page, pageSize, totalCount, totalPages }` pagination envelope
- Gateway auth policy is named `"authenticated"` — all routes require it except public ones explicitly marked
- Email restriction: only `.edu` / `.edu.vn` domains allowed for registration
- AppRole enum: `Student`, `Lecturer`, `Admin`, `Service` (internal service-to-service only)
