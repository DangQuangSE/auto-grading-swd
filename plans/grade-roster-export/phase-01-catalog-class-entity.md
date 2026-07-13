# Phase 1: Catalog `Class` Entity & Events

## Requirements

Create a new `Class` domain entity in Catalog service with Id, Name, LecturerId, and CreatedAt fields. Implement POST (admin-only create), PATCH (admin-only lecturer reassign), and GET (anonymous, Id+Name only) endpoints. Publish a `ClassLecturerAssigned` integration event whenever a Class is created or its LecturerId changes. This event is the source of truth that Identity uses to build its local class-name cache (Phase 2).

## Steps

1. Add a new domain entity `Class` to Catalog with fields: Id (Guid), Name (string, required), LecturerId (Guid, required), and CreatedAt (DateTimeOffset). Decide on database constraints (e.g., unique index on Name or not — spec does not require it, so allow duplicates for now).

2. Update `CatalogDbContext` to include a `DbSet<Class>` for the new table.

3. Create an EF Core migration named `AddClassEntity` (or similar) that adds the Class table schema with the above fields and an index on LecturerId for fast lookups during authorization queries.

4. Define a new integration event record `ClassLecturerAssigned(ClassId, ClassName, LecturerId)` in the Contracts assembly (mirroring the pattern from `RubricConfirmed.cs`). Ensure it inherits from `IntegrationEvent` base class.

5. Create a new endpoint group `ClassesEndpoints` (following the Catalog pattern from RubricsEndpoints) with three mapped routes:
   - POST /classes (create, admin-only, accepts a form with Name + LecturerId, returns created Class object)
   - PATCH /classes/{id} (reassign LecturerId, admin-only, returns updated Class)
   - GET /classes (list-all, **NO auth required**, returns only Id + Name to anonymous callers, no LecturerId)

6. In the POST and PATCH handlers, after saving the updated Class to the database, publish the `ClassLecturerAssigned` event via the event bus.

7. Create xUnit tests covering: successful create with event publish; successful reassign with event publish; GET /classes returns only Id+Name (no LecturerId); unauthorized (lecturer) POST returns 403; unauthorized (lecturer) PATCH returns 403.

8. Register the endpoints in Catalog's Program.cs (e.g., `app.MapClassesEndpoints();`).

9. Run the migration locally and verify the Class table is created and indexes are applied.

10. Test the endpoints manually via curl or Postman: create a Class, verify the event was published (by checking the event bus logs or by subscribing from another service), verify GET /classes omits LecturerId.

## Success Criteria

- `Class` domain entity exists in Catalog with Id, Name, LecturerId, CreatedAt
- `CatalogDbContext` includes `DbSet<Class>`
- EF Core migration `AddClassEntity` creates the table and indexes
- `ClassLecturerAssigned(ClassId, ClassName, LecturerId)` event record exists in Contracts
- POST /classes endpoint creates a Class and publishes the event
- PATCH /classes/{id} endpoint reassigns LecturerId and publishes the event
- GET /classes endpoint returns Id+Name only (no LecturerId) to anonymous callers
- All role-based authorization works (admin allowed, lecturer forbidden for POST/PATCH)
- Unit tests pass (create with event, reassign with event, anonymous GET, auth checks)
- Catalog service compiles and starts without errors
- Manual test: create a Class, fetch it via GET /classes, verify no LecturerId in response

## Risks

- **Event Publishing Failure** — If the event bus is down when a POST/PATCH request completes, the event is lost and Identity's cache (Phase 2) never learns about the new/updated Class. *Mitigation:* The event publish is inside the transaction; if publish fails, the DB save is rolled back, and the endpoint returns an error. Ensure logging captures the failure so admins can diagnose and retry.
- **Anonymous GET Leaks Class Count** — Exposing GET /classes (even without LecturerId) reveals the total number of classes in the system to anonymous users. *Mitigation:* This is acceptable per spec (needed for registration form); if privacy is a concern later, add rate-limiting or time-based access restrictions.
- **Duplicate Class Names** — Two lecturers might create classes with the same name, causing confusion in the UI. *Mitigation:* Spec does not require uniqueness; add a comment in the code noting this. A future iteration can add a constraint if needed.

