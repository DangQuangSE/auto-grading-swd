# Deployment

This project is designed for React/Vite plus Supabase.

## Required Environment Variables

Frontend:

- `VITE_SUPABASE_URL`
- `VITE_SUPABASE_ANON_KEY`

Supabase Edge Functions:

- `SUPABASE_URL`
- `SUPABASE_SERVICE_ROLE_KEY`
- `OPENROUTER_API_KEY`
- `OPENROUTER_MODEL`
- `APP_PUBLIC_URL`

## Local Checks

```bash
npm test
npm run build
```

## Supabase Setup

1. Create a Supabase project.
2. Apply migrations from `supabase/migrations`.
3. Configure storage buckets from `0002_storage_policies.sql`.
4. Set Edge Function secrets.
5. Deploy functions:

```bash
supabase functions deploy extract-submission
supabase functions deploy grade-submission
```

## OpenRouter

The grading function calls OpenRouter Chat Completions with a strict JSON response request.

Recommended starting settings:

- Low temperature such as `0.1`.
- A model with strong instruction following.
- Store raw response metadata for audit.

## Operational Notes

- Original files, extracted artifacts, AI suggestions, final lecturer scores, and publish events are stored separately.
- Students should only see final grades after publication.
- Service role keys must never be exposed to the frontend.
- School data-retention policy still needs a final decision before production.
