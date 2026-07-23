using System.Security.Claims;
using AutoGrading.Catalog.Api.Domain;
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
        IClassRepository repo,
        CancellationToken cancellationToken)
    {
        var includeLecturerId = caller.IsInRole("admin") || caller.IsInRole("lecturer");
        var classes = await repo.ListAsync(cancellationToken);

        return Results.Ok(classes
            .Select(item => new LegacyClassSummary(item.Id, item.Name, includeLecturerId ? item.LecturerId : null))
            .ToList());
    }

    private static async Task<IResult> ListAdminClassesAsync(
        int? page,
        int? pageSize,
        Guid? subjectId,
        IClassRepository repo,
        CancellationToken cancellationToken)
    {
        var result = await repo.ListAdminAsync(subjectId, page, pageSize, cancellationToken);
        var items = result.Items.Select(ClassSummary.From).ToList();

        return Results.Ok(new PagedResult<ClassSummary>(items, result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<IResult> ListSubjectClassesAsync(
        Guid subjectId,
        int? page,
        int? pageSize,
        ClaimsPrincipal caller,
        ISubjectRepository subjectRepo,
        IClassRepository classRepo,
        CancellationToken cancellationToken)
    {
        var subject = await subjectRepo.GetByIdAsync(subjectId, cancellationToken);
        if (subject is null || (caller.IsInRole("student") && subject.RegistrationStatus != RegistrationStatus.Open))
        {
            return Results.NotFound();
        }

        var result = await classRepo.ListForSubjectAsync(subjectId, page, pageSize, cancellationToken);
        var items = result.Items.Select(item => new RegistrationClassOption(item.Id, item.Name, subjectId)).ToList();

        return Results.Ok(new PagedResult<RegistrationClassOption>(items, result.Page, result.PageSize, result.TotalCount));
    }

    private static Task<IResult> CreateLegacyClassAsync(
        CreateLegacyClassRequest request,
        ISubjectRepository subjectRepo,
        IClassRepository classRepo,
        CancellationToken cancellationToken) =>
        CreateClassCoreAsync(request.Name, request.LecturerId, null, subjectRepo, classRepo, cancellationToken);

    private static Task<IResult> CreateSubjectScopedClassAsync(
        CreateSubjectScopedClassRequest request,
        ISubjectRepository subjectRepo,
        IClassRepository classRepo,
        CancellationToken cancellationToken) =>
        CreateClassCoreAsync(request.Name, request.LecturerId, request.SubjectId, subjectRepo, classRepo, cancellationToken);

    private static async Task<IResult> CreateClassCoreAsync(
        string? name,
        Guid lecturerId,
        Guid? subjectId,
        ISubjectRepository subjectRepo,
        IClassRepository classRepo,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(name);
        var validationError = ValidateClassInput(normalizedName, lecturerId, subjectId);
        if (validationError is not null)
        {
            return validationError;
        }

        if (subjectId.HasValue && !await subjectRepo.AnyAsync(subjectId.Value, cancellationToken))
        {
            return Results.BadRequest(new { code = "invalid_subject", message = "Subject does not exist." });
        }

        var @class = new Class
        {
            Name = name!.Trim(),
            NormalizedName = normalizedName!,
            LecturerId = lecturerId,
            SubjectId = subjectId
        };
        if (subjectId.HasValue)
        {
            @class.EnrollmentSubjectId = subjectId.Value;
        }

        try
        {
            @class = await classRepo.CreateAsync(@class, cancellationToken);
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(new { code = ex.Code, message = ex.Message });
        }
        catch (ClassEventPublishException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Created($"/classes/{@class.Id}", ClassSummary.From(@class));
    }

    private static async Task<IResult> UpdateClassAsync(
        Guid id,
        UpdateClassRequest request,
        ISubjectRepository subjectRepo,
        IClassRepository classRepo,
        CancellationToken cancellationToken)
    {
        if (!request.LecturerId.HasValue && !request.SubjectId.HasValue)
        {
            return Results.BadRequest(new { code = "empty_update", message = "No class change was provided." });
        }

        var @class = await classRepo.GetByIdAsync(id, cancellationToken);
        if (@class is null)
        {
            return Results.NotFound();
        }

        if (request.LecturerId.HasValue)
        {
            if (request.LecturerId == Guid.Empty)
            {
                return Results.BadRequest(new { code = "invalid_lecturer", message = "LecturerId is required." });
            }

            @class.LecturerId = request.LecturerId.Value;
        }

        if (request.SubjectId.HasValue && request.SubjectId != @class.SubjectId)
        {
            if (request.SubjectId == Guid.Empty || !await subjectRepo.AnyAsync(request.SubjectId.Value, cancellationToken))
            {
                return Results.BadRequest(new { code = "invalid_subject", message = "Subject does not exist." });
            }

            if (await classRepo.AnyWithEnrollmentsAsync(id, cancellationToken))
            {
                return Results.Conflict(new
                {
                    code = "class_subject_locked",
                    message = "A class with enrollments cannot be moved to another subject."
                });
            }

            @class.SubjectId = request.SubjectId.Value;
            @class.EnrollmentSubjectId = request.SubjectId.Value;
        }

        try
        {
            @class = await classRepo.UpdateAsync(@class, cancellationToken);
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(new { code = ex.Code, message = ex.Message });
        }
        catch (ClassEventPublishException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(ClassSummary.From(@class));
    }

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToUpperInvariant();

    private static IResult? ValidateClassInput(string? normalizedName, Guid lecturerId, Guid? subjectId)
    {
        if (normalizedName is null)
        {
            return Results.BadRequest(new { code = "invalid_class_name", message = "Name is required." });
        }

        if (normalizedName.Length > 256)
        {
            return Results.BadRequest(new { code = "class_name_too_long", message = "Name must be at most 256 characters." });
        }

        if (lecturerId == Guid.Empty)
        {
            return Results.BadRequest(new { code = "invalid_lecturer", message = "LecturerId is required." });
        }

        if (subjectId == Guid.Empty)
        {
            return Results.BadRequest(new { code = "invalid_subject", message = "SubjectId is required." });
        }

        return null;
    }
}

public sealed record LegacyClassSummary(Guid Id, string Name, Guid? LecturerId);

public sealed record ClassSummary(Guid Id, string Name, Guid LecturerId, Guid? SubjectId, string? SubjectCode)
{
    public static ClassSummary From(Class item) =>
        new(item.Id, item.Name, item.LecturerId, item.SubjectId, item.Subject?.Code);
}

public sealed record RegistrationClassOption(Guid Id, string Name, Guid SubjectId);

public sealed record CreateLegacyClassRequest(string? Name, Guid LecturerId);

public sealed record CreateSubjectScopedClassRequest(string? Name, Guid LecturerId, Guid SubjectId);

public sealed record UpdateClassRequest(Guid? LecturerId, Guid? SubjectId);
