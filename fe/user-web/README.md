# AutoGrading — User Web

Student/lecturer-facing app (React + Vite + TypeScript). Talks to the backend exclusively
through the YARP API Gateway (`be/src/Gateway`) — see `../../requirment.md` for the full
architecture.

## Running locally

```bash
npm install
cp .env.example .env.local   # set VITE_API_BASE_URL to the gateway, e.g. http://localhost:5500
npm run dev
```

Full-stack (backend + this app) is easiest via the root `docker-compose.yml` — see the repo
root README.

## Scripts

```bash
npm run dev        # Vite dev server
npm run build       # tsc -b && vite build
npm test             # vitest run
npm run lint         # eslint
```

## Key structure

- `src/lib/apiClient.ts` — REST client against the gateway (JWT bearer auth).
- `src/services/` — per-domain API calls (auth, subjects, assignments, rubrics, submissions, grading).
- `src/pages/` — routed pages (login/register, dashboards, submission upload/detail, grading review).
