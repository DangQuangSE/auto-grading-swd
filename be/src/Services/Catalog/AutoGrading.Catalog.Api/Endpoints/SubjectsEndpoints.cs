using System.Data;
using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

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
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Subjects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(subject => subject.Code.Contains(term) || subject.Name.Contains(term));
        }

        return Results.Ok(await ToPagedResultAsync(query, normalizedPage, normalizedPageSize, cancellationToken));
    }

    private static async Task<IResult> ListOpenSubjectsAsync(
        int? page,
        int? pageSize,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Subjects.AsNoTracking()
            .Where(subject => subject.RegistrationStatus == RegistrationStatus.Open);

        return Results.Ok(await ToPagedResultAsync(query, normalizedPage, normalizedPageSize, cancellationToken));
    }

    private static async Task<PagedResult<SubjectSummary>> ToPagedResultAsync(
        IQueryable<Subject> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(subject => subject.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(subject => new SubjectSummary(
                subject.Id,
                subject.Code,
                subject.Name,
                subject.RegistrationStatus,
                subject.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<SubjectSummary>(items, page, pageSize, totalCount);
    }

    private static async Task<IResult> CreateSubjectAsync(
        CreateSubjectRequest request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { code = "invalid_subject", message = "Code and name are required." });
        }

        if (code.Length > 32 || name.Length > 256)
        {
            return Results.BadRequest(new
            {
                code = "subject_length_exceeded",
                message = "Code must be at most 32 characters and name at most 256 characters."
            });
        }

        var subject = new Subject
        {
            Code = code,
            Name = name,
            RegistrationStatus = RegistrationStatus.Closed
        };
        db.Subjects.Add(subject);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { code = "subject_code_exists", message = "Subject code already exists." });
        }

        return Results.Created($"/subjects/{subject.Id}", SubjectSummary.From(subject));
    }

    private static async Task<IResult> UpdateRegistrationAsync(
        Guid id,
        UpdateSubjectRegistrationRequest request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return Results.BadRequest(new { code = "invalid_registration_status", message = "Status must be open or closed." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var subject = await db.Subjects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null)
        {
            return Results.NotFound();
        }

        subject.RegistrationStatus = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(SubjectSummary.From(subject));
    }
}

public sealed record SubjectSummary(
    Guid Id,
    string Code,
    string Name,
    RegistrationStatus RegistrationStatus,
    DateTimeOffset CreatedAt)
{
    public static SubjectSummary From(Subject subject) => new(
        subject.Id,
        subject.Code,
        subject.Name,
        subject.RegistrationStatus,
        subject.CreatedAt);
}

public sealed record CreateSubjectRequest(string? Code, string? Name);

public sealed record UpdateSubjectRegistrationRequest(RegistrationStatus Status);
