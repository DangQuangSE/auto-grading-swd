# Phase 5: Event Contract & Grading Consumer

## Requirements

Implement the event consumer side in the Grading service: create a handler that subscribes to the `RubricConfirmed` event from Catalog, deserializes the event payload, and upserts the confirmed criteria into a new local table in `GradingDbContext` (keyed by `RubricId` for idempotency). Create the local criteria table, add a database migration, and register the handler in the Grading service's startup.

## Steps

1. Add a new entity `LocalRubricCriteria` (or similar name) to the Grading domain to represent a local copy of confirmed criteria; this entity should have `RubricId` as a unique key, plus fields for the criteria list (JSON array or child collection).
2. Update `GradingDbContext` in the Grading service to include a `DbSet<LocalRubricCriteria>` for the new table.
3. Create a new event handler class `RubricConfirmedHandler` in `be/src/Services/Grading/AutoGrading.Grading.Api/Infrastructure/Handlers/` that implements the event bus's handler interface.
4. In the handler's execute method, deserialize the incoming `RubricConfirmed` event, query the local table by `RubricId`, and if a row exists, update it with the new criteria; if not, insert a new row. Ensure this upsert logic is idempotent (safe to run multiple times with the same event payload without creating duplicates or orphaned data). Note: if a rubric is edited and re-confirmed (unlock → edit → confirm again), the resulting `RubricConfirmed` event carries the new criteria and this same upsert overwrites the local row for that `RubricId` — already-graded submissions keep their previously recorded scores; no automatic re-grading is triggered (out of scope, see spec.md).
5. Create an EF Core migration for the Grading service that adds the new local criteria table with a unique constraint on `RubricId`.
6. Run the migration in a local dev environment and verify the table is created correctly.
7. Register the handler in `GradingProgram.cs` using the pattern `eventBus.Subscribe<RubricConfirmed, RubricConfirmedHandler>()` after `app.Build()` (following the existing `ArtifactsExtractedHandler` registration pattern).
8. Verify the handler is called by publishing a test event from Catalog and checking that a new row appears in the Grading database.
9. Create a new xUnit test project `AutoGrading.Grading.Api.Tests` (`be/src/Services/Grading/AutoGrading.Grading.Api.Tests/`, using an EF Core InMemory or SQLite-in-memory provider for `GradingDbContext`), added to the solution. Add `RubricConfirmedHandlerTests` covering idempotency: handling the same `RubricConfirmed` event payload twice results in exactly one row for that `RubricId` with the latest criteria (no duplicate rows, no exception on the second delivery).

## Success Criteria

- `LocalRubricCriteria` entity exists in Grading domain
- `GradingDbContext` includes the new table via `DbSet<LocalRubricCriteria>`
- `RubricConfirmedHandler` exists and implements the event bus handler interface
- Migration creates the local criteria table with a unique index on `RubricId`
- Handler upserts criteria idempotently (re-delivering the same event does not duplicate rows)
- Handler is registered in `Program.cs` and runs after `app.Build()`
- Publishing a `RubricConfirmed` event results in a new row in the local table
- Grading service compiles and starts without errors
- `AutoGrading.Grading.Api.Tests` project exists, is part of the solution, and `dotnet test` passes for `RubricConfirmedHandlerTests`, proving double-delivery of the same event does not duplicate rows

## Risks

- **Event delivery failure** — If the RabbitMQ event bus is down or the event is lost in transit, the handler never runs and Grading's local copy is never populated. Grading's `AiGradingJob` will later fail because it cannot find any criteria. *Mitigation:* This is an accepted architectural risk (user confirmed); add clear logging in the handler so administrators can diagnose missing criteria; consider a future admin endpoint to manually replay events.
- **Handler exception on bad event data** — If the event payload is malformed or missing required fields, the handler might throw an exception and fail to upsert. *Mitigation:* Implement defensive parsing in the handler (check for null fields, use Try-* patterns); log any extraction anomalies and let the handler gracefully skip bad events.
- **Database constraint violation** — If the handler attempts to insert/update and hits a unique constraint violation on `RubricId`, the handler fails. *Mitigation:* Use EF's `ExecuteUpdate` or raw SQL for an upsert operation (e.g., `INSERT INTO ... ON CONFLICT DO UPDATE` pattern or EF's `AddOrUpdate` if available) to ensure atomicity.

---
