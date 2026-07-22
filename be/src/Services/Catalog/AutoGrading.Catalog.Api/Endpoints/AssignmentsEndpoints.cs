using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class AssignmentsEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assignments").WithTags("Assignments");

        group.MapGet("/", async (Guid? subjectId, int? page, int? pageSize, CatalogDbContext db, CancellationToken ct) =>
            {
                var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);

                var query = db.Assignments.AsNoTracking().AsQueryable();
                if (subjectId is not null)
                {
                    query = query.Where(a => a.SubjectId == subjectId);
                }

                var totalCount = await query.CountAsync(ct);
                var items = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToListAsync(ct);

                return Results.Ok(new PagedResult<Assignment>(items, normalizedPage, normalizedPageSize, totalCount));
            })
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db, CancellationToken ct) =>
            {
                var assignment = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
                return assignment is null ? Results.NotFound() : Results.Ok(assignment);
            })
            .RequireAuthorization();

        group.MapPost("/", async (CreateAssignmentRequest request, CatalogDbContext db, CancellationToken ct) =>
            {
                if (request.MaxAttempts < 1) return Results.BadRequest(new { error = "MaxAttempts must be at least 1." });
                var assignment = new Assignment
                {
                    SubjectId = request.SubjectId,
                    Title = request.Title,
                    Description = request.Description,
                    DueDate = request.DueDate,
                    MaxAttempts = request.MaxAttempts,
                };
                db.Assignments.Add(assignment);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/assignments/{assignment.Id}", assignment);
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPut("/{id:guid}", async (Guid id, UpdateAssignmentRequest request, CatalogDbContext db, CancellationToken ct) =>
            {
                if (request.MaxAttempts < 1) return Results.BadRequest(new { error = "MaxAttempts must be at least 1." });
                var assignment = await db.Assignments.FirstOrDefaultAsync(a => a.Id == id, ct);
                if (assignment is null) return Results.NotFound();
                assignment.Title = request.Title;
                assignment.Description = request.Description;
                assignment.DueDate = request.DueDate;
                assignment.MaxAttempts = request.MaxAttempts;
                await db.SaveChangesAsync(ct);
                return Results.Ok(assignment);
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }
}

public sealed record CreateAssignmentRequest(Guid SubjectId, string Title, string? Description, DateTimeOffset? DueDate, int MaxAttempts = 1);
public sealed record UpdateAssignmentRequest(string Title, string? Description, DateTimeOffset? DueDate, int MaxAttempts);
