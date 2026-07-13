using System.Security.Claims;
using AutoGrading.Identity.Api.Authorization;
using AutoGrading.Identity.Api.Data;
using AutoGrading.Identity.Api.Domain;
using AutoGrading.Identity.Api.RosterImport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users");

        group.MapGet("/", async (string[]? ids, IdentityDbContext db, CancellationToken ct) =>
            {
                var requestedIds = ParseIds(ids);
                var users = requestedIds is null
                    ? await db.Users.AsNoTracking().ToListAsync(ct)
                    : await db.Users.AsNoTracking().Where(u => requestedIds.Contains(u.Id)).ToListAsync(ct);

                return Results.Ok(await ResolveClassNamesAsync(users, db, ct));
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPatch("/{userId:guid}", UpdateUserAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/bulk-import", BulkImportAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"))
            .DisableAntiforgery();

        return app;
    }

    /// <summary>Bulk-updates StudentCode/ClassId from an uploaded roster file (.xlsx/.xls/.csv) with
    /// header-mapped columns Email, StudentCode, ClassName (case-insensitive; StudentCode may be blank
    /// to leave it unchanged). Each row is authorized independently via <see cref="RosterAuthorization"/>
    /// — a lecturer can only update rows for students they have a relationship with. All accepted rows are
    /// persisted in a single SaveChangesAsync call; if that fails, no row is updated. Processes the file
    /// synchronously on the request thread; sized for class-scale rosters (~20-50 rows), not bulk imports —
    /// if usage regularly exceeds a few hundred rows, move this to a background job instead.</summary>
    private static async Task<IResult> BulkImportAsync(
        [FromForm] BulkImportForm form,
        ClaimsPrincipal caller,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        RosterFileParseResult parseResult;
        await using (var stream = form.File.OpenReadStream())
        {
            parseResult = RosterFileParser.Parse(stream, form.File.FileName);
        }

        if (parseResult.Error is not null)
        {
            return Results.BadRequest(new { message = parseResult.Error });
        }

        var details = new List<RosterImportRowResult>();
        var updatedCount = 0;

        foreach (var row in parseResult.Rows)
        {
            var email = row.Email.Trim().ToLowerInvariant();
            var className = row.ClassName.Trim();

            var classCache = await db.ClassLecturerCaches
                .FirstOrDefaultAsync(c => c.ClassName.ToLower() == className.ToLower(), cancellationToken);
            if (classCache is null)
            {
                details.Add(new RosterImportRowResult(row.RowNumber, email, "skipped", "unknown class"));
                continue;
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
            if (user is null)
            {
                details.Add(new RosterImportRowResult(row.RowNumber, email, "skipped", "email not registered"));
                continue;
            }

            var authorization = await RosterAuthorization.AuthorizeAsync(caller, user, db, cancellationToken);
            if (authorization == RosterAuthorizationResult.Denied)
            {
                details.Add(new RosterImportRowResult(row.RowNumber, email, "skipped", "not authorized for this student"));
                continue;
            }

            if (row.StudentCode is not null)
            {
                user.StudentCode = row.StudentCode;
            }

            user.ClassId = classCache.ClassId;

            details.Add(new RosterImportRowResult(row.RowNumber, email, "updated", null));
            updatedCount++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Problem(
                "Failed to save roster import; no rows were updated. Please retry.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new RosterImportReport(parseResult.Rows.Count, updatedCount, parseResult.Rows.Count - updatedCount, details));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        ClaimsPrincipal caller,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (target is null)
        {
            return Results.NotFound();
        }

        var authorization = await RosterAuthorization.AuthorizeAsync(caller, target, db, cancellationToken);
        if (authorization == RosterAuthorizationResult.Denied)
        {
            return Results.Forbid();
        }

        if (request.ClassId is { } classId && !await db.ClassLecturerCaches.AnyAsync(c => c.ClassId == classId, cancellationToken))
        {
            return Results.BadRequest(new { message = "Class not found or not yet synchronized; please try again or contact your administrator." });
        }

        if (request.StudentCode is not null)
        {
            target.StudentCode = request.StudentCode;
        }

        if (request.ClassId is not null)
        {
            target.ClassId = request.ClassId;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { message = $"User {userId} was modified concurrently; reload and try again." });
        }

        var summary = (await ResolveClassNamesAsync([target], db, cancellationToken)).Single();
        return Results.Ok(summary);
    }

    private static HashSet<Guid>? ParseIds(string[]? ids)
    {
        if (ids is not { Length: > 0 })
        {
            return null;
        }

        var parsed = ids
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(token => Guid.TryParse(token, out _))
            .Select(Guid.Parse)
            .ToHashSet();

        return parsed.Count > 0 ? parsed : null;
    }

    private static async Task<List<UserSummary>> ResolveClassNamesAsync(List<User> users, IdentityDbContext db, CancellationToken cancellationToken)
    {
        var classIds = users.Where(u => u.ClassId is not null).Select(u => u.ClassId!.Value).Distinct().ToList();
        var classNames = await db.ClassLecturerCaches
            .Where(c => classIds.Contains(c.ClassId))
            .ToDictionaryAsync(c => c.ClassId, c => c.ClassName, cancellationToken);

        return users
            .Select(u => new UserSummary(
                u.Id,
                u.Email,
                u.FullName,
                u.Role.ToString().ToLowerInvariant(),
                u.StudentCode,
                u.ClassId,
                u.ClassId is { } classId && classNames.TryGetValue(classId, out var className) ? className : null))
            .ToList();
    }
}

public sealed record UserSummary(Guid Id, string Email, string FullName, string Role, string? StudentCode, Guid? ClassId, string? ClassName);

public sealed record UpdateUserRequest(string? StudentCode, Guid? ClassId);

public sealed class BulkImportForm
{
    public IFormFile File { get; set; } = null!;
}

public sealed record RosterImportRowResult(int RowNumber, string Email, string Status, string? Reason);

public sealed record RosterImportReport(int TotalRows, int UpdatedCount, int SkippedCount, IReadOnlyList<RosterImportRowResult> Details);
