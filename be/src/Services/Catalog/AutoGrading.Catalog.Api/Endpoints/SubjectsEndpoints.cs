using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
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
        ISubjectRepository repo,
        CancellationToken cancellationToken)
    {
        var result = await repo.ListAsync(search, page, pageSize, cancellationToken);
        return Results.Ok(ToSummaryPage(result));
    }

    private static async Task<IResult> ListOpenSubjectsAsync(
        int? page,
        int? pageSize,
        ISubjectRepository repo,
        CancellationToken cancellationToken)
    {
        var result = await repo.ListOpenAsync(page, pageSize, cancellationToken);
        return Results.Ok(ToSummaryPage(result));
    }

    private static PagedResult<SubjectSummary> ToSummaryPage(PagedResult<Subject> result) =>
        new(result.Items.Select(SubjectSummary.From).ToList(), result.Page, result.PageSize, result.TotalCount);

    private static async Task<IResult> CreateSubjectAsync(
        CreateSubjectRequest request,
        ISubjectRepository repo,
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

        try
        {
            subject = await repo.CreateAsync(subject, cancellationToken);
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(new { code = ex.Code, message = ex.Message });
        }

        return Results.Created($"/subjects/{subject.Id}", SubjectSummary.From(subject));
    }

    private static async Task<IResult> UpdateRegistrationAsync(
        Guid id,
        UpdateSubjectRegistrationRequest request,
        ISubjectRepository repo,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return Results.BadRequest(new { code = "invalid_registration_status", message = "Status must be open or closed." });
        }

        var subject = await repo.UpdateRegistrationAsync(id, request.Status, cancellationToken);
        return subject is null ? Results.NotFound() : Results.Ok(SubjectSummary.From(subject));
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
