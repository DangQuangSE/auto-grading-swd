# Phase 5: Update Background Jobs

## Requirements

Decouple `RubricParsingJob` from `CatalogDbContext` to `IRubricRepository`. Mirror the precedent set by Submission's `ExtractionJob` (Phase 5) and Grading's `AiGradingJob` (Phase 5).

## Design Constraints

- `RubricParsingJob` must inject `IRubricRepository` instead of `CatalogDbContext`.
- The job's orchestration (download from MinIO, extract text, call AI, update criteria, transition status) stays intact — only the data-access calls change from `db.Rubrics.Include(...).FirstOrDefaultAsync(...)` to `repo.GetByIdAsync(...)`, etc.
- `Jobs/DocxTextExtractor.cs` is a pure static utility (no DbContext dependency) — verify it's unchanged and compiles correctly.
- No new repository methods required — the existing methods from Phase 2 (`GetByIdAsync`, `UpdateAsync`, `UpdateCriteriaAsync`) are sufficient.

## Steps

1. Verify `Jobs/DocxTextExtractor.cs` is a pure static utility with zero DbContext dependencies (already confirmed in prep phase: lines 8–19 are pure text extraction from OpenXml, no EF Core) — re-check this explicitly during implementation, don't just trust the prep-phase note, since a future drift here would silently reintroduce a DbContext dependency into Jobs/. Update namespace/using if the file moved, otherwise leave unchanged.

2. Update `Jobs/RubricParsingJob.cs`:
   - Remove `CatalogDbContext db` injection parameter (line 16).
   - Add `IRubricRepository repo` injection parameter.
   - Line 24–29: replace `await db.Rubrics.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.Id == rubricId, cancellationToken)` with `await repo.GetByIdAsync(rubricId, includeCriteria: true, cancellationToken)`.
   - Line 56–63: replace `db.ReplaceRubricCriteria(rubric, ...)` (custom DbContext helper) with a repository call. Phase 2's `IRubricRepository.UpdateCriteriaAsync` should handle this; if not, add `UpdateCriteriaAsync(rubric, criteria, cancellationToken)` to the repository interface and implement it.
   - Line 65–66: replace `db.SaveChangesAsync(cancellationToken)` with `await repo.UpdateAsync(rubric, cancellationToken)` (or a narrower update method if needed).
   - Line 68–70: event publishing via `eventBus` stays unchanged — the job already injects `IEventBus` (line 19).

3. Update `Program.cs` DI: `RubricParsingJob` already has `AddScoped<RubricParsingJob>()` registered (line 26). No change needed — Hangfire will inject `IRubricRepository` automatically since it's registered. Verify the registration is there and correct.

4. Delete the confirmed-dead `Parsing/DocxRubricParser.cs` and `Parsing/IRubricParser.cs` (175 lines combined, zero external references verified via `grep -rln "DocxRubricParser\|IRubricParser"` — only the files' own self-references matched). This is a deliberate zero-risk cleanup documented in `plan.md`'s Deliberate Design Decisions, not incidental scope creep. Remove the `Parsing/` folder entirely if these were its only contents. Re-run the grep after deletion to confirm zero remaining references anywhere in the solution (including `Program.cs` DI registrations, if any).

## Success Criteria

- `dotnet build` on `AutoGrading.Catalog.Api` compiles with zero errors.
- `Jobs/RubricParsingJob.cs` injects `IRubricRepository` and calls it instead of `CatalogDbContext`.
- No EF Core types (`DbContext`, `DbSet`, `DbUpdateException`) appear in `Jobs/` files (grep search).
- Rubric upload + Hangfire job execution works end-to-end (manual test).
- `Parsing/DocxRubricParser.cs` and `Parsing/IRubricParser.cs` are deleted; `grep -rln "DocxRubricParser\|IRubricParser"` returns zero matches solution-wide.

## Quality and Testing State

- Quality gate: not evaluated (Cook runs `/ck:quality --gate` after implementing this phase)
- Testing: not started

## Manual Verification

1. `dotnet build` — zero errors.
2. `grep -rn "DbContext\|DbSet\|DbUpdateException\|DbUpdateConcurrencyException" Jobs/` returns nothing.
2a. `grep -rln "DocxRubricParser\|IRubricParser"` returns nothing solution-wide (dead code fully removed).
3. `grep -n "using AutoGrading.Catalog.Api.Data\|using AutoGrading.Catalog.Api.Repository" Jobs/RubricParsingJob.cs` confirms namespace is updated to `Repository` if `CatalogDbContext` is still in that namespace.
4. Upload a rubric via `POST /rubrics/upload` and verify end-to-end:
   - File is uploaded to MinIO.
   - Metadata is persisted (status = Parsing).
   - Hangfire job is enqueued and executes.
   - Job calls `repo.GetByIdAsync`, fetches text from MinIO, calls AI parser, updates criteria via `repo.UpdateCriteriaAsync` or similar, transitions status to Draft.
   - `RubricParsed` event is published (check event logs).
   - Rubric status is now `Draft` and criteria are populated (verify via `GET /rubrics/{id}`).
