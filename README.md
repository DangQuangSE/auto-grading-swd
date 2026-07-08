# Auto Grading

Serverless web platform for grading IT project reports and architecture diagrams.

The MVP is designed around:

- React + Vite + TypeScript for the web app.
- Supabase Auth, Postgres, Storage, and Edge Functions.
- Word `.docx` rubric templates with standardized grading tables.
- Student `.docx` reports and `.drawio` architecture diagrams.
- OpenRouter-backed AI grading suggestions.
- Lecturer-approved final grades stored separately from AI suggestions.

## Local Setup

```bash
npm install
npm run dev
```

## Environment

Copy `.env.example` to `.env.local` and fill in the relevant values.

- `VITE_SUPABASE_URL`: Supabase project URL used by the web app.
- `VITE_SUPABASE_ANON_KEY`: Supabase anonymous public key used by the web app.
- `SUPABASE_SERVICE_ROLE_KEY`: service key used only by trusted serverless functions.
- `OPENROUTER_API_KEY`: API key used by grading functions.
- `OPENROUTER_MODEL`: model identifier for criterion-level grading.

## Supabase

Migrations live in `supabase/migrations`.

Edge Functions:

- `extract-submission`: parses rubric Word files, student Word reports, and Draw.io XML diagrams.
- `grade-submission`: sends extracted artifacts and rubric criteria to OpenRouter, then stores AI criterion scores.

## Rubric and Submission Docs

- `docs/rubric-docx-format.md`
- `docs/submission-template-guidelines.md`
- `docs/deployment.md`

## Current Status

Implemented so far:

- React/Vite TypeScript scaffold.
- Supabase schema and storage policy migrations.
- Shared domain and validation models.
- Edge Function scaffolds for extraction and AI grading.
- Frontend service layer and workflow orchestration.
- Dashboard, upload, review, and result UI screens.
- Focused Vitest coverage for validation, parsers, review service behavior, and review UI.

Run local checks:

```bash
npm test
npm run build
```

Remaining product hardening is tracked in `plans/serverless-auto-grading/plan.json`.
