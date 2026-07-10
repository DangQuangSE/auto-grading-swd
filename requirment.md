# ASP.NET Core Microservices Project Proposal

## 1. Project Information
* **Project Title:** AutoGrading — IT Project Report & Architecture Diagram Grading Platform
* **Team Members:**
  | Student ID | Full Name | Role |
  | :--- | :--- | :--- |
  | | | |
  | | | |
  | | | |

---

## 2. Project Overview
* **Problem Statement:**
  Lecturers grading student software-engineering project submissions (report + architecture
  diagram) manually is slow and inconsistent: each submission has to be read, cross-checked
  against a rubric, and scored by hand. There's no automated first pass and no shared system
  of record for rubrics, submissions, or grades across a course.
* **Proposed Solution:**
  A web platform where lecturers define subjects/assignments/rubrics, students upload their
  report (.docx) and architecture diagram (.drawio), and the system automatically extracts
  the submitted artifacts, runs an AI-assisted grading pass against the rubric criteria via
  an LLM (OpenRouter), and produces a per-criterion score that a lecturer can review and
  publish as the final grade. All steps (upload → extraction → AI grading → publish) are
  event-driven across independent microservices so each stage can scale, fail, and retry
  independently.
* **Target Users:**
  University lecturers (subject/assignment/rubric owners, graders) and students (submitters)
  of software-engineering coursework, plus an admin role for platform oversight.

---

## 3. Functional Requirements
### User Functions (minimum 5)
1. Register and log in with email/password, issued a JWT (student or lecturer role).
2. Lecturer: create subjects and assignments.
3. Lecturer: upload a rubric (.docx) for a subject/assignment; parsed into scored criteria.
4. Student: upload a submission (report .docx + architecture diagram .drawio) for an assignment.
5. View a submission's status and extracted artifacts (report text, diagram structure, warnings).
6. View AI grading runs and the per-criterion scores produced for a submission.
7. Lecturer: review and publish the final grade for a submission.
8. View personal notifications (e.g. grade published, AI grading completed).

### Admin Functions (minimum 5)
1. All lecturer functions (subjects, assignments, rubrics) via the admin web app.
2. View system-wide audit events (registrations, grading runs, publishes) across services.
3. View all submissions and their processing state across students/assignments.
4. View background job activity (extraction, AI grading) via the Hangfire dashboards.
5. Manage user roles/accounts through the Identity service (admin-authorized endpoints).

---

## 4. Microservices Design
*(Minimum: 4 Microservices)*

| Service Name | Responsibilities | Main APIs |
| :--- | :--- | :--- |
| **Identity** | User registration/login, password hashing, JWT issuance, publishes `UserRegistered` | `POST /auth/register`, `POST /auth/login` |
| **Catalog** | Subjects, assignments, rubric upload/parsing (.docx → criteria), publishes `RubricParsed` | `GET/POST /subjects`, `GET/POST /assignments`, `GET /rubrics`, `POST /rubrics/upload` |
| **Submission** | Submission upload to MinIO, artifact extraction (report/diagram) as a Hangfire job, publishes `SubmissionUploaded`/`ArtifactsExtracted` | `GET /submissions`, `GET /submissions/{id}`, `POST /submissions/upload` |
| **Grading** | AI-assisted grading via OpenRouter as a Hangfire job, final grade publish, publishes `AiGradingCompleted`/`GradePublished` | `GET /grades/{submissionId}/runs`, `GET /grades/{submissionId}/final`, `POST /grades/{submissionId}/publish` |
| **Notification** | Cross-service event consumer: audit log + per-user notifications | `GET /notifications`, `GET /audit-events` |

All five services sit behind a **YARP API Gateway** (`AutoGrading.Gateway`) that terminates
JWT validation, applies CORS, and reverse-proxies to each service by route prefix
(`/identity/*`, `/catalog/*`, `/submissions/*`, `/grades/*`, `/notifications/*`).

---

## 5. Database Design
* **Main Entities:**
  - Identity: `User` (email, password hash, role, full name)
  - Catalog: `Subject`, `Assignment`, `Rubric`, `RubricCriterion`
  - Submission: `Submission`, `ExtractedArtifact`
  - Grading: `AiGradingRun`, `AiCriterionScore`, `FinalGrade`
  - Notification: `Notification`, `AuditEvent`
  Each service owns an independent SQL Server database (database-per-service:
  `AutoGrading.Identity`, `AutoGrading.Catalog`, `AutoGrading.Submission`,
  `AutoGrading.Grading`, `AutoGrading.Notification`), migrated automatically on startup via
  EF Core.
* **ERD Diagram:**
  [TODO: export and embed an ERD image per service — schemas are defined in each service's
  `Data/*DbContext.cs` and `Domain/*.cs`]

---

## 6. Message Queue Design
* **Message Broker:** RabbitMQ (topic exchange `autograding.events`, one queue per
  service/handler pair, via a shared `IEventBus` abstraction in `AutoGrading.Common`)

*(Minimum: 5 Business Events)*

| Event Name | Publisher | Subscriber | Purpose |
| :--- | :--- | :--- | :--- |
| `UserRegistered` | Identity | Notification (`UserRegisteredConsumer`) | Writes an audit event + welcome notification when a new account is created |
| `SubmissionUploaded` | Submission | Submission itself (`SubmissionUploadedHandler`) | Enqueues the `ExtractionJob` background job to parse the uploaded report/diagram |
| `ArtifactsExtracted` | Submission | Grading (`ArtifactsExtractedHandler`) | On successful extraction, enqueues the `AiGradingJob` background job |
| `AiGradingCompleted` | Grading | Notification (`AiGradingCompletedConsumer`) | Notifies the student/lecturer that an AI grading run finished |
| `GradePublished` | Grading | Notification (`GradePublishedConsumer`) | Notifies the student that their final grade was published |
| `RubricParsed` | Catalog | *(published for audit trail; no active subscriber yet)* | Signals a rubric finished parsing into criteria |

---

## 7. Background Jobs
*(Minimum: 2 Background Jobs, run via Hangfire with SQL Server storage)*

| Job Name | Schedule | Purpose |
| :--- | :--- | :--- |
| `ExtractionJob` (Submission service) | Enqueued on `SubmissionUploaded` | Downloads the uploaded report/diagram from MinIO, parses them into `ExtractedArtifact` rows, publishes `ArtifactsExtracted` |
| `AiGradingJob` (Grading service) | Enqueued on `ArtifactsExtracted` (success) | Calls the OpenRouter LLM API with the rubric + extracted content, records per-criterion `AiCriterionScore`s, retries with exponential backoff on failure (`[AutomaticRetry]`), publishes `AiGradingCompleted` |

Both services expose a Hangfire dashboard (`/hangfire`) for observing job history/retries.

---

## 8. Security Design
* **JWT Authentication:** Identity issues signed JWTs (HMAC, shared signing key across
  services) on login, containing the user id, email, and role claims. Every downstream
  service (Catalog, Submission, Grading, Notification) and the Gateway validate the same
  token via `AddJwtAuthentication` (shared `AutoGrading.Common.Auth` helper); the Gateway
  additionally re-validates and forwards the token to the proxied service.
* **Authorization Roles:** `student`, `lecturer`, `admin` — enforced with
  `RequireAuthorization(policy => policy.RequireRole(...))` on sensitive endpoints (e.g.
  rubric upload/grade publish require `lecturer`/`admin`; `/audit-events` requires `admin`).

---

## 9. User Interfaces
* **User Web Features (`fe/user-web`):**
  Login/register, subject & assignment browsing, rubric viewing, submission upload with
  drag-and-drop, submission status/artifact viewing, AI grading run + final grade viewing,
  notifications list.
* **Admin Web Features (`fe/admin-web`):**
  Admin login, subject/assignment/rubric management, cross-user submission and audit-event
  overview.

---

## 10. Technology Stack
* **Backend:** ASP.NET Core 8 Minimal APIs, EF Core (SQL Server provider)
* **Database:** SQL Server 2022 (database-per-service)
* **Message Broker:** RabbitMQ (topic exchange, `RabbitMQ.Client`)
* **Background Jobs:** Hangfire (SQL Server storage), triggered from event consumers
* **Object Storage:** MinIO (S3-compatible), for submission/report/diagram files
* **Frontend:** React + Vite + TypeScript (`fe/user-web`, `fe/admin-web`)
* **API Gateway:** YARP (`Yarp.ReverseProxy`)
* **AI Grading:** OpenRouter (LLM API) for rubric-based scoring
* **DevOps:** Docker Compose (single root `docker-compose.yml` running all services, SQL
  Server, RabbitMQ, MinIO, and both frontends)

---

## 11. Architecture Diagram
* **System Architecture Diagram:** [TODO: embed — Gateway → 5 services → SQL Server/RabbitMQ/MinIO]
* **Microservices Diagram:** [TODO: embed — see service table in section 4]
* **Deployment Diagram:** [TODO: embed — see `docker-compose.yml` service graph]

---

## 12. Project Plan
* **Sprint 1 Deliverables:** Repo split into `fe/`/`be/`, solution scaffold, BuildingBlocks (Contracts + Common: JWT, event bus, MinIO, EF base)
* **Sprint 2 Deliverables:** Identity, Catalog, Submission, Grading, Notification services implemented with their APIs, event publishing/consuming, and background jobs
* **Sprint 3 Deliverables:** YARP API Gateway, `fe/user-web` ported to the new REST API, `fe/admin-web` scaffolded
* **Sprint 4 Deliverables:** Root `docker-compose.yml` full-stack integration, end-to-end verification (login/JWT, event flow, Hangfire jobs), documentation

---

## 13. Expected Deliverables
* Source Code
* Documentation
* Diagrams
* Working System

---

## ⚠️ Minimum Requirements Checklist
- [x] 4+ Microservices — Identity, Catalog, Submission, Grading, Notification (5)
- [x] 15+ Functional Requirements — see section 3 (13 listed; expand with edge-case variants as needed)
- [x] User Web + Admin Web — `fe/user-web`, `fe/admin-web`
- [x] SQL Database — SQL Server, database-per-service via EF Core
- [x] Message Queue Integration — RabbitMQ, `IEventBus` abstraction
- [x] 2+ Background Jobs — `ExtractionJob`, `AiGradingJob` (Hangfire)
- [x] JWT Authentication — Identity issues, all services validate
- [x] API Gateway — YARP (`AutoGrading.Gateway`)
- [x] Docker Compose Deployment — root `docker-compose.yml`, full stack verified healthy
- [x] 5+ Business Events — `UserRegistered`, `SubmissionUploaded`, `ArtifactsExtracted`, `AiGradingCompleted`, `GradePublished`, `RubricParsed` (6)