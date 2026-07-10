# AutoGrading — Admin Web

Minimal admin app (React + Vite + TypeScript) for platform oversight: subject/assignment/
rubric management, cross-user submission and audit-event visibility. Talks to the same YARP
API Gateway as `fe/user-web` — see `../../requirment.md` for the full architecture.

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
npm run lint         # eslint
```

Requires a logged-in user with the `admin` role (issued by the Identity service) to access
role-gated endpoints such as `/audit-events`.
