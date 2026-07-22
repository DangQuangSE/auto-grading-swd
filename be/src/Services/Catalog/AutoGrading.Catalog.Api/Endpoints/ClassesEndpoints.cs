using System.Security.Claims;
using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

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
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var includeLecturerId = caller.IsInRole("admin") || caller.IsInRole("lecturer");
        var classes = await db.Classes.AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new LegacyClassSummary(
                item.Id,
                item.Name,
                includeLecturerId ? item.LecturerId : null))
            .ToListAsync(cancellationToken);

        return Results.Ok(classes);
    }

    private static async Task<IResult> ListAdminClassesAsync(
        int? page,
        int? pageSize,
        Guid? subjectId,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Classes.AsNoTracking();
        if (subjectId.HasValue)
        {
            query = query.Where(item => item.SubjectId == subjectId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(item => new ClassSummary(
                item.Id,
                item.Name,
                item.LecturerId,
                item.SubjectId,
                item.Subject == null ? null : item.Subject.Code))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResult<ClassSummary>(items, normalizedPage, normalizedPageSize, totalCount));
    }

    private static async Task<IResult> ListSubjectClassesAsync(
        Guid subjectId,
        int? page,
        int? pageSize,
        ClaimsPrincipal caller,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.AsNoTracking()
            .Where(item => item.Id == subjectId)
            .Select(item => new { item.RegistrationStatus })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject is null || (caller.IsInRole("student") && subject.RegistrationStatus != RegistrationStatus.Open))
        {
            return Results.NotFound();
        }

        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Classes.AsNoTracking().Where(item => item.SubjectId == subjectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(item => new RegistrationClassOption(item.Id, item.Name, subjectId))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResult<RegistrationClassOption>(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount));
    }

    private static Task<IResult> CreateLegacyClassAsync(
        CreateLegacyClassRequest request,
        CatalogDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken) =>
        CreateClassCoreAsync(request.Name, request.LecturerId, null, db, eventBus, cancellationToken);

    private static Task<IResult> CreateSubjectScopedClassAsync(
        CreateSubjectScopedClassRequest request,
        CatalogDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken) =>
        CreateClassCoreAsync(request.Name, request.LecturerId, request.SubjectId, db, eventBus, cancellationToken);

    private static async Task<IResult> CreateClassCoreAsync(
        string? name,
        Guid lecturerId,
        Guid? subjectId,
        CatalogDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(name);
        var validationError = ValidateClassInput(normalizedName, lecturerId, subjectId);
        if (validationError is not null)
        {
            return validationError;
        }

        if (subjectId.HasValue && !await db.Subjects.AnyAsync(subject => subject.Id == subjectId, cancellationToken))
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
        db.Classes.Add(@class);

        var publishError = await SaveAndPublishAsync(db, eventBus, @class, cancellationToken);
        return publishError ?? Results.Created($"/classes/{@class.Id}", ClassSummary.From(@class));
    }

    private static async Task<IResult> UpdateClassAsync(
        Guid id,
        UpdateClassRequest request,
        CatalogDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        if (!request.LecturerId.HasValue && !request.SubjectId.HasValue)
        {
            return Results.BadRequest(new { code = "empty_update", message = "No class change was provided." });
        }

        var @class = await db.Classes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
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
            if (request.SubjectId == Guid.Empty ||
                !await db.Subjects.AnyAsync(subject => subject.Id == request.SubjectId, cancellationToken))
            {
                return Results.BadRequest(new { code = "invalid_subject", message = "Subject does not exist." });
            }

            if (await db.StudentEnrollments.AnyAsync(enrollment => enrollment.ClassId == id, cancellationToken))
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

        var publishError = await SaveAndPublishAsync(db, eventBus, @class, cancellationToken);
        return publishError ?? Results.Ok(ClassSummary.From(@class));
    }

    private static async Task<IResult?> SaveAndPublishAsync(
        CatalogDbContext db,
        IEventBus eventBus,
        Class @class,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await eventBus.PublishAsync(
                new ClassLecturerAssigned(@class.Id, @class.Name, @class.LecturerId),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { code = "class_conflict", message = "Class data conflicts with an existing class." });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(
                "Failed to publish ClassLecturerAssigned event; the class change was not saved. Please retry.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
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
