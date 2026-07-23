using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Dto;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class SubjectsEndpoints
{
    public static IEndpointRouteBuilder MapSubjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/subjects").WithTags("Subjects");

        group.MapGet("/", ListSubjectsAsync).RequireAuthorization();
        group.MapGet("/open-for-registration", ListOpenSubjectsAsync)
            .RequireAuthorization(policy => policy.RequireRole("student"));
        group.MapPost("/", CreateSubjectAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));
        group.MapPatch("/{id:guid}/registration", UpdateRegistrationAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));

        return app;
    }

    private static async Task<IResult> ListSubjectsAsync(
        int? page,
        int? pageSize,
        string? search,
        ISubjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(search, page, pageSize, cancellationToken);
        return Results.Ok(ToSummaryPage(result));
    }

    private static async Task<IResult> ListOpenSubjectsAsync(
        int? page,
        int? pageSize,
        ISubjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListOpenAsync(page, pageSize, cancellationToken);
        return Results.Ok(ToSummaryPage(result));
    }

    private static PagedResult<SubjectResponse> ToSummaryPage(PagedResult<Subject> result) =>
        result.MapItems(SubjectResponse.FromDomain);

    private static async Task<IResult> CreateSubjectAsync(
        CreateSubjectRequest request,
        ISubjectService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = await service.CreateAsync(request.Code, request.Name, cancellationToken);
            return Results.Created($"/subjects/{subject.Id}", SubjectResponse.FromDomain(subject));
        }
        catch (CatalogValidationException ex)
        {
            return Results.BadRequest(new { code = ex.Code, message = ex.Message });
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(new { code = ex.Code, message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateRegistrationAsync(
        Guid id,
        UpdateSubjectRegistrationRequest request,
        ISubjectService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = await service.UpdateRegistrationAsync(id, request.Status, cancellationToken);
            return subject is null ? Results.NotFound() : Results.Ok(SubjectResponse.FromDomain(subject));
        }
        catch (CatalogValidationException ex)
        {
            return Results.BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }
}
