# Phase 4: Catalog Edit, Confirm & Unlock Endpoints

## Requirements

Add three new endpoints to the Catalog Rubrics controller/handler: one to edit criteria (only in Draft state), one to confirm a rubric (Draft → Confirmed, publishing a domain event), and one to unlock a confirmed rubric back to draft state for further editing. Enforce authorization rules (owner/admin can edit their own; admin-only for SchoolWide scope creation). Create the `RubricConfirmed` event contract.

## Steps

1. Create the `RubricConfirmed` event contract in `be/src/BuildingBlocks/AutoGrading.Contracts/Events/RubricConfirmed.cs` following the existing `RubricParsed.cs` pattern as a public record with fields: `RubricId`, `SubjectId`, `AssignmentId`, `Scope`, and a full list of criteria (name, description, max score, order).
2. Add a `PATCH /rubrics/{id}/criteria` endpoint that accepts a list of updated criteria, verifies the rubric's `Status` is `Draft`, checks authorization (current user is the rubric's owner or admin), updates the criteria in-memory, persists to the database, and returns the updated list.
3. Add a `POST /rubrics/{id}/confirm` endpoint that verifies `Status` is `Draft`, checks authorization, transitions `Status` to `Confirmed`, publishes the `RubricConfirmed` event on the domain event bus with the full rubric and criteria payload, and returns a success response.
4. Add a `POST /rubrics/{id}/unlock` endpoint that verifies `Status` is `Confirmed`, checks authorization (owner or admin), transitions `Status` back to `Draft`, and returns a success response. This endpoint should not re-publish any event.
5. Ensure authorization checks use the rubric's `LecturerId` to determine ownership for `Lecturer` scope; `SchoolWide` rubrics can only be confirmed/unlocked/edited by the `admin` role.
6. Add a `RowVersion` (EF Core concurrency token, `byte[]` with `[Timestamp]` or `IsRowVersion()` in `OnModelCreating`) to the `Rubric` entity in Phase 2's domain changes. In the criteria-edit, confirm, and unlock endpoints, catch `DbUpdateConcurrencyException` from `SaveChangesAsync` and return `409 Conflict` if the rubric was modified concurrently.
7. Verify the confirm endpoint publishes the event by checking the domain event outbox (if the codebase uses event sourcing) or the direct event bus publish call.
8. Test end-to-end: create a draft rubric, edit its criteria, confirm, and verify the status changes and event is published.

## Success Criteria

- `RubricConfirmed` event contract exists at `be/src/BuildingBlocks/AutoGrading.Contracts/Events/RubricConfirmed.cs`
- `PATCH /rubrics/{id}/criteria` endpoint exists and only accepts edits when `Status = Draft`
- `POST /rubrics/{id}/confirm` endpoint exists, transitions `Confirmed`, and publishes `RubricConfirmed` event
- `POST /rubrics/{id}/unlock` endpoint exists, transitions back to `Draft`, and allows re-editing
- Authorization checks prevent non-owners from editing/confirming their own rubrics
- Admin can confirm `SchoolWide` rubrics; non-admin users cannot
- A confirmed rubric cannot be edited without first calling unlock
- A `Confirmed` rubric remains usable for grading
- Two concurrent requests modifying the same rubric (e.g. confirm + unlock at once) result in one succeeding and the other receiving `409 Conflict`, not a corrupted state

## Risks

- **Authorization bypass** — If authorization checks are incomplete, a non-owner might edit or confirm a rubric they don't own. *Mitigation:* Use the existing role-based authorization middleware (e.g., `[Authorize(Roles = "lecturer,admin")]`); verify in each endpoint that the current user is either the `LecturerId` or has `admin` role.
- **Event publish failure** — If the event is not successfully published (e.g., message bus is down), the confirm endpoint may return success but Grading never receives the event. *Mitigation:* This is accepted as a known limitation (user confirmed); rely on the event bus to log the failure; future versions should add an admin endpoint to manually retry publication.
- **Concurrent confirm/unlock race** — If confirm and unlock are called simultaneously, the rubric state machine could enter an inconsistent state. *Mitigation:* Use optimistic locking (EF Core's `RowVersion` or similar) to detect and reject concurrent modifications.

---
