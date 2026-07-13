# Phase 2: Catalog Domain & Status Machine

## Requirements

Extend the `Rubric` domain entity with three new fields (`Status`, `Scope`, `LecturerId`) to track parsing state, scope, and ownership; add an `Unlock` action to enable moving confirmed rubrics back to draft state for re-editing; persist these changes to the database and ensure existing rubrics are safely backfilled.

## Steps

1. Define two new enums in the Catalog domain: `RubricStatus` (values: `Parsing`, `Draft`, `Confirmed`) and `RubricScope` (values: `Lecturer`, `SchoolWide`).
2. Add four new fields to the `Rubric` entity: `Status` (required, defaults to `Parsing` on creation), `Scope` (required, defaults to `Lecturer`), `LecturerId` (nullable Guid, represents the owning lecturer; null for `SchoolWide` rubrics), and `RowVersion` (`byte[]`, EF Core concurrency token via `[Timestamp]` or `.IsRowVersion()`, used by Phase 4's confirm/unlock/edit endpoints to reject concurrent modifications with `409 Conflict`).
3. Add an `Unlock()` method to the `Rubric` entity that transitions `Status` from `Confirmed` back to `Draft`.
4. Update the `CatalogDbContext.OnModelCreating()` method to configure the new fields: ensure `Status` and `Scope` are persisted as strings (or integers) with appropriate indexes, set `LecturerId` as nullable, configure `RowVersion` as a concurrency token.
5. Create an EF Core migration (e.g., `AddRubricStatusScopeOwner.cs`) that adds the four columns to the `Rubric` table. Existing `Rubric`/`RubricCriterion` rows are demo data only (per user confirmation) — the migration should delete all existing rows from `Rubrics` and `RubricCriteria` before/while adding the new non-nullable `Status`/`Scope` columns, rather than building backfill logic for legacy rows that have no `LecturerId` and were never confirmed through the new flow. This keeps the migration simple and avoids leaving orphaned "Confirmed"-but-ownerless rubrics that Grading never received an event for.
6. Run the migration in a local dev environment and verify the schema changes apply cleanly and the `Rubrics`/`RubricCriteria` tables are empty afterward (existing demo rubrics must be re-uploaded through the new flow to get real AI-parsed criteria).

## Success Criteria

- `RubricStatus` and `RubricScope` enums exist and are used in the `Rubric` entity
- `Rubric` entity has `Status`, `Scope`, and `LecturerId` properties
- `Rubric` has an `Unlock()` method that transitions `Confirmed` → `Draft`
- Migration file exists and clears existing `Rubrics`/`RubricCriteria` rows (demo data) rather than backfilling them
- Running the migration applies successfully without errors
- Catalog service compiles after the domain model changes

## Risks

- **Existing demo rubrics are deleted by the migration** — Confirmed acceptable by the user (current rubric rows are demo data only); lecturers must re-upload any rubric they still need after this migration runs.
- **Enum serialization mismatches** — If the API previously returned `Rubric` objects, the new enums may cause JSON serialization issues if not properly configured. *Mitigation:* Ensure the enums are configured in any API response DTOs and that the JSON serializer (System.Text.Json or Newtonsoft) is set to serialize enum values as strings or integers consistently.

---
