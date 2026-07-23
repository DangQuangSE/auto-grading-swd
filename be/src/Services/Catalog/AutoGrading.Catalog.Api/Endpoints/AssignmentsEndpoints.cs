using AutoGrading.Catalog.Api.Dto;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class AssignmentsEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assignments").WithTags("Assignments");

        group.MapGet("/", async (Guid? subjectId, int? page, int? pageSize, IAssignmentService service, CancellationToken ct) =>
            {
                var result = await service.ListAsync(subjectId, page, pageSize, ct);
                return Results.Ok(result.MapItems(AssignmentResponse.FromDomain));
            })
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, IAssignmentService service, CancellationToken ct) =>
            {
                var assignment = await service.GetByIdAsync(id, ct);
                return assignment is null ? Results.NotFound() : Results.Ok(AssignmentResponse.FromDomain(assignment));
            })
            .RequireAuthorization();

        group.MapPost("/", async (CreateAssignmentRequest request, IAssignmentService service, CancellationToken ct) =>
            {
                try
                {
                    var assignment = await service.CreateAsync(
                        request.SubjectId, request.Title, request.Description, request.DueDate, request.MaxAttempts, ct);
                    return Results.Created($"/assignments/{assignment.Id}", AssignmentResponse.FromDomain(assignment));
                }
                catch (CatalogValidationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPut("/{id:guid}", async (Guid id, UpdateAssignmentRequest request, IAssignmentService service, CancellationToken ct) =>
            {
                try
                {
                    var assignment = await service.UpdateAsync(
                        id, request.Title, request.Description, request.DueDate, request.MaxAttempts, ct);
                    return assignment is null ? Results.NotFound() : Results.Ok(AssignmentResponse.FromDomain(assignment));
                }
                catch (CatalogValidationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }
}
