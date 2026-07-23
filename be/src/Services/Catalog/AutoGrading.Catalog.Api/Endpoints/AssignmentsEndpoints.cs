using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class AssignmentsEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assignments").WithTags("Assignments");

        group.MapGet("/", async (Guid? subjectId, int? page, int? pageSize, IAssignmentRepository repo, CancellationToken ct) =>
                Results.Ok(await repo.ListAsync(subjectId, page, pageSize, ct)))
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, IAssignmentRepository repo, CancellationToken ct) =>
            {
                var assignment = await repo.GetByIdAsync(id, ct);
                return assignment is null ? Results.NotFound() : Results.Ok(assignment);
            })
            .RequireAuthorization();

        group.MapPost("/", async (CreateAssignmentRequest request, IAssignmentRepository repo, CancellationToken ct) =>
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
                assignment = await repo.CreateAsync(assignment, ct);
                return Results.Created($"/assignments/{assignment.Id}", assignment);
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPut("/{id:guid}", async (Guid id, UpdateAssignmentRequest request, IAssignmentRepository repo, CancellationToken ct) =>
            {
                if (request.MaxAttempts < 1) return Results.BadRequest(new { error = "MaxAttempts must be at least 1." });
                var assignment = await repo.UpdateAsync(id, request.Title, request.Description, request.DueDate, request.MaxAttempts, ct);
                return assignment is null ? Results.NotFound() : Results.Ok(assignment);
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }
}

public sealed record CreateAssignmentRequest(Guid SubjectId, string Title, string? Description, DateTimeOffset? DueDate, int MaxAttempts = 1);
public sealed record UpdateAssignmentRequest(string Title, string? Description, DateTimeOffset? DueDate, int MaxAttempts);
