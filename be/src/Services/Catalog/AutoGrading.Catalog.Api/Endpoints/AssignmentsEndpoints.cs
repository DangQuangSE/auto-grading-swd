using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class AssignmentsEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assignments").WithTags("Assignments");

        group.MapGet("/", async (Guid? subjectId, CatalogDbContext db, CancellationToken ct) =>
            {
                var query = db.Assignments.AsNoTracking().AsQueryable();
                if (subjectId is not null)
                {
                    query = query.Where(a => a.SubjectId == subjectId);
                }

                return Results.Ok(await query.ToListAsync(ct));
            })
            .RequireAuthorization();

        group.MapPost("/", async (CreateAssignmentRequest request, CatalogDbContext db, CancellationToken ct) =>
            {
                var assignment = new Assignment
                {
                    SubjectId = request.SubjectId,
                    Title = request.Title,
                    Description = request.Description,
                    DueDate = request.DueDate,
                };
                db.Assignments.Add(assignment);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/assignments/{assignment.Id}", assignment);
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }
}

public sealed record CreateAssignmentRequest(Guid SubjectId, string Title, string? Description, DateTimeOffset? DueDate);
