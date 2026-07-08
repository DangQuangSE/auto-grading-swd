# Brainstorm: Serverless IT Project Auto-Grading Platform

**Date:** 2026-07-08

## Ideas Explored

- MVP serverless grading pipeline: React uploads student `.docx`, `.drawio`, and lecturer rubric `.docx` files to Supabase Storage; Supabase Edge Functions extract, grade, and persist results.
- Rubric-first grading: AI scores each criterion/question separately, explains deductions, and returns evidence-backed comments instead of only a total score.
- Rule-based plus AI-based evaluation: deterministic parsers validate template structure, required sections, and diagram entities; AI evaluates semantic quality and consistency with the rubric.
- Human-in-the-loop review: AI provides suggested scores and comments, but lecturers approve or edit final scores before publishing.
- Multi-course template support: each subject has its own rubric template uploaded by the lecturer, allowing SWD, SWR, and future subjects to evolve independently.

## User's Direction

The user wants this to be a real grading tool for their school, not a demo. The chosen direction is criterion-by-criterion grading: AI produces suggested scores, comments, and deductions per question/criterion, then the lecturer decides the official score.

For rubric templates, the user wants lecturers to select a subject and upload that subject's template. The user clarified that these subjects commonly use Word files, so the recommended first format is a constrained rubric `.docx` containing standardized grading tables, normalized internally into structured JSON.

## Open Questions

- Exact rubric Word table columns need to be finalized before implementation.
- Official supported subjects for MVP need to be selected, though the architecture should support multiple subjects.
- The minimum audit trail required by the school is not yet defined.

## Risks

- AI scoring may be inconsistent unless prompts require structured evidence, deductions, and bounded scores per criterion.
- Word and Draw.io template drift can break extraction if deterministic validation is too rigid or too loose.
- Serverless execution limits may be a constraint for large `.docx` files, complex diagrams, or batch grading.
