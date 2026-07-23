using System.Security.Claims;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Dto;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class ClassesEndpoints
{
    public static IEndpointRouteBuilder MapClassesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/classes").WithTags("Classes");

        group.MapGet("/", ListLegacyClassesAsync).AllowAnonymous();
        group.MapGet("/admin", ListAdminClassesAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));
        group.MapGet("/by-subject/{subjectId:guid}", ListSubjectClassesAsync)
            .RequireAuthorization();
        group.MapPost("/", CreateLegacyClassAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));
        group.MapPost("/subject-scoped", CreateSubjectScopedClassAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));
        group.MapPatch("/{id:guid}", UpdateClassAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));

        return app;
    }

    private static async Task<IResult> ListLegacyClassesAsync(
        ClaimsPrincipal caller,
        IClassService service,
        CancellationToken cancellationToken)
    {
        var includeLecturerId = caller.IsInRole("admin") || caller.IsInRole("lecturer");
        var classes = await service.ListLegacyAsync(cancellationToken);

        return Results.Ok(classes
            .Select(item => new LegacyClassSummary(item.Id, item.Name, includeLecturerId ? item.LecturerId : null))
            .ToList());
    }

    private static async Task<IResult> ListAdminClassesAsync(
        int? page,
        int? pageSize,
        Guid? subjectId,
        IClassService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAdminAsync(subjectId, page, pageSize, cancellationToken);

        return Results.Ok(result.MapItems(ClassSummary.FromDomain));
    }

    private static async Task<IResult> ListSubjectClassesAsync(
        Guid subjectId,
        int? page,
        int? pageSize,
        ClaimsPrincipal caller,
        IClassService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ListForSubjectAsync(subjectId, page, pageSize, caller.IsInRole("student"), cancellationToken);

            return Results.Ok(result.MapItems(item => new RegistrationClassOption(item.Id, item.Name, subjectId)));
        }
        catch (CatalogNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static Task<IResult> CreateLegacyClassAsync(
        CreateLegacyClassRequest request,
        IClassService service,
        CancellationToken cancellationToken) =>
        CreateClassCoreAsync(() => service.CreateLegacyAsync(request.Name, request.LecturerId, cancellationToken));

    private static Task<IResult> CreateSubjectScopedClassAsync(
        CreateSubjectScopedClassRequest request,
        IClassService service,
        CancellationToken cancellationToken) =>
        CreateClassCoreAsync(() => service.CreateSubjectScopedAsync(request.Name, request.LecturerId, request.SubjectId, cancellationToken));

    private static async Task<IResult> CreateClassCoreAsync(Func<Task<Class>> createAsync)
    {
        try
        {
            var @class = await createAsync();
            return Results.Created($"/classes/{@class.Id}", ClassSummary.FromDomain(@class));
        }
        catch (CatalogValidationException ex)
        {
            return Results.BadRequest(new { code = ex.Code, message = ex.Message });
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(new { code = ex.Code, message = ex.Message });
        }
        catch (ClassEventPublishException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> UpdateClassAsync(
        Guid id,
        UpdateClassRequest request,
        IClassService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var @class = await service.UpdateAsync(id, request.LecturerId, request.SubjectId, cancellationToken);
            return Results.Ok(ClassSummary.FromDomain(@class));
        }
        catch (CatalogNotFoundException)
        {
            return Results.NotFound();
        }
        catch (CatalogValidationException ex)
        {
            return Results.BadRequest(new { code = ex.Code, message = ex.Message });
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(new { code = ex.Code, message = ex.Message });
        }
        catch (ClassEventPublishException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
