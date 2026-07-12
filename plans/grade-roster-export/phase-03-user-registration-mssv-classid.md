# Phase 3: User Registration with MSSV & ClassId

## Requirements

Add `StudentCode` and `ClassId` fields to the User entity in Identity, create a database migration, and update the POST /auth/register endpoint to accept optional `studentCode` and `classId` request fields. Validate that if `classId` is provided, it exists in the local ClassLecturerCache (populated by Phase 2); reject with 400 if the class is unknown. Persist both fields on the created User.

## Steps

1. Add two new nullable properties to the `User` domain entity: `StudentCode` (string, nullable, no length constraint in this phase) and `ClassId` (Guid, nullable, no cross-database FK).

2. Update `IdentityDbContext` to add property mappings for both fields (e.g., `.Property(u => u.StudentCode).IsRequired(false).HasMaxLength(50)` and `.Property(u => u.ClassId).IsRequired(false)`). Add an index on ClassId for fast lookups during authorization checks.

3. Create an EF Core migration `AddStudentCodeAndClassIdToUser` that adds both columns to the Users table.

4. Update the `RegisterRequest` record in `AuthEndpoints.cs` to include optional `StudentCode` (string, nullable) and `ClassId` (Guid, nullable) fields.

5. In the `RegisterAsync` handler, before creating the User, validate the `classId` if provided: query `db.ClassLecturerCache.FirstOrDefaultAsync(c => c.ClassId == classId)`. If not found, return `Results.BadRequest(new { message = "Class not found or not yet synchronized; please try again or contact your administrator." })`. If found, assign the values to the User entity.

6. When persisting the User, assign `user.StudentCode = request.StudentCode` and `user.ClassId = request.ClassId` (both can be null; that's OK).

7. Update the POST /auth/google handler similarly: if a registration is triggered from Google login and `classId` is not provided (because the Google flow doesn't capture it), that's acceptable; the student's class can be set later via PATCH /users/{userId} in Phase 4.

8. Create xUnit tests covering: successful registration with StudentCode and ClassId (valid class); registration with ClassId that doesn't exist in cache (returns 400 with "Class not found" message); registration without StudentCode/ClassId (both fields null, registration succeeds); idempotent re-registration (same email again fails with 409 conflict, not 400).

9. Run the migration locally and verify both columns are added to the Users table.

10. Manual test: register a student with a valid ClassId; fetch the student via an admin endpoint; verify StudentCode and ClassId are persisted correctly.

## Success Criteria

- `User` entity has `StudentCode` (string, nullable) and `ClassId` (Guid, nullable) properties
- `IdentityDbContext` includes mappings for both new properties
- EF Core migration `AddStudentCodeAndClassIdToUser` adds both columns
- POST /auth/register accepts optional `studentCode` and `classId` fields in the request
- Registration validates `classId` against ClassLecturerCache (returns 400 if unknown)
- Successful registration persists both fields on the User
- Registration with null `studentCode`/`classId` succeeds (both optional)
- Unit tests pass all scenarios (valid class, unknown class, null values, conflict on re-register)
- Identity service compiles and starts without errors
- Manual test: register with valid class, verify fields are stored

## Risks

- **Cache Lag on Registration** — If a Class is created in Catalog but ClassLecturerAssignedHandler hasn't yet processed it in Identity, a registration specifying that classId fails with 400 (race condition). *Mitigation:* Spec accepts this as a rare race; add clear error message guiding user to retry. Implement client-side retry with exponential backoff in Phase 11 (user-web).
- **No Uniqueness on StudentCode** — Two users can have the same StudentCode, causing confusion in reports. *Mitigation:* Spec explicitly allows this ("MSSV has no enforced uniqueness"); add a code comment noting this. Add a unique constraint if the school's MSSV format is known later.
- **ClassId Not Validated at Write Time** — The ClassId is validated against cache at registration, but if a user's ClassId is later set to a non-existent value (e.g., cache dropped a row), there's no foreign-key constraint to catch it. *Mitigation:* Caches are event-driven and eventually consistent; trust the cache is authoritative. Add monitoring/alerts if cache queries fail.

