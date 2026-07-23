using System.Security.Claims;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Catalog.Api.Jobs;
using AutoGrading.Common.Auth;
using AutoGrading.Common.Messaging;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class RubricsEndpoints
{
    public static IEndpointRouteBuilder MapRubricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rubrics").WithTags("Rubrics");

        group.MapGet("/", async (Guid? subjectId, Guid? assignmentId, ClaimsPrincipal user, IRubricRepository repo, CancellationToken ct) =>
                Results.Ok(await repo.ListAsync(subjectId, assignmentId, user.GetUserId(), user.IsInRole("admin"), ct)))
            .RequireAuthorization();

        group.MapGet("/{id:guid}/file", async (Guid id, ClaimsPrincipal user, IRubricRepository repo, IObjectStorage storage, CancellationToken ct) =>
            {
                var rubric = await repo.DownloadFileAsync(id, ct);
                if (rubric?.FileObjectKey is null)
                {
                    return Results.NotFound();
                }

                if (!CanView(rubric, user))
                {
                    return Results.Forbid();
                }

                var stream = await storage.DownloadAsync(rubric.FileObjectKey, ct);
                return Results.File(
                    stream,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    rubric.Name);
            })
            .RequireAuthorization();

        group.MapPost("/upload", UploadRubricAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"))
            .DisableAntiforgery();

        group.MapPost("/{id:guid}/retry-parsing", RetryParsingAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPatch("/{id:guid}/criteria", UpdateCriteriaAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/{id:guid}/confirm", ConfirmRubricAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/{id:guid}/unlock", UnlockRubricAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }

    private static async Task<IResult> UploadRubricAsync(
        [FromForm] UploadRubricForm form,
        ClaimsPrincipal user,
        IRubricRepository repo,
        IObjectStorage storage,
        IBackgroundJobClient backgroundJobs,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();

        if (form.Scope == RubricScope.SchoolWide && !user.IsInRole("admin"))
        {
            return Results.Forbid();
        }

        var existingRubric = form.AssignmentId is null
            ? null
            : await repo.GetByAssignmentIdAsync(form.AssignmentId.Value, cancellationToken);

        if (existingRubric is not null && !IsAuthorized(existingRubric, user))
        {
            return Results.Forbid();
        }

        var objectKey = $"rubrics/{Guid.NewGuid()}-{form.File.FileName}";
        await using (var stream = form.File.OpenReadStream())
        {
            await storage.UploadAsync(objectKey, stream, form.File.ContentType, cancellationToken);
        }

        Rubric rubric;
        if (existingRubric is not null)
        {
            if (!string.IsNullOrEmpty(existingRubric.FileObjectKey))
            {
                await storage.DeleteAsync(existingRubric.FileObjectKey, cancellationToken);
            }

            existingRubric.Name = form.Name;
            existingRubric.FileObjectKey = objectKey;
            existingRubric.Status = RubricStatus.Parsing;

            // Deliberate Phase 2 simplification: the original endpoint clears criteria and saves the field
            // changes in one SaveChanges call. Splitting into two repository calls (clear, then update) costs
            // one extra round-trip but keeps Repository/Endpoint boundaries clean; low risk since re-upload
            // isn't a concurrent hot path and RubricParsingJob overwrites criteria again moments later anyway.
            await repo.UpdateCriteriaAsync(existingRubric, new List<RubricCriterion>(), cancellationToken);
            rubric = await repo.UpdateAsync(existingRubric, cancellationToken);
        }
        else
        {
            rubric = new Rubric
            {
                SubjectId = form.SubjectId,
                AssignmentId = form.AssignmentId,
                Name = form.Name,
                FileObjectKey = objectKey,
                Scope = form.Scope,
                LecturerId = form.Scope == RubricScope.SchoolWide ? null : userId,
            };
            rubric = await repo.CreateAsync(rubric, cancellationToken);
        }

        backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(rubric.Id, CancellationToken.None));

        return Results.Created($"/rubrics/{rubric.Id}", rubric);
    }

    private static async Task<IResult> RetryParsingAsync(
        Guid id,
        ClaimsPrincipal user,
        IRubricRepository repo,
        IBackgroundJobClient backgroundJobs,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, repo, includeCriteria: false, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (rubric!.Status != RubricStatus.Parsing)
        {
            return Results.Conflict($"Rubric {id} is '{rubric.Status}', not 'Parsing' — re-upload instead of retrying.");
        }

        backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(rubric.Id, CancellationToken.None));

        return Results.Accepted();
    }

    private static async Task<IResult> UpdateCriteriaAsync(
        Guid id,
        List<UpdateCriterionRequest> request,
        ClaimsPrincipal user,
        IRubricRepository repo,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, repo, includeCriteria: true, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (rubric!.Status != RubricStatus.Draft)
        {
            return Results.Conflict($"Rubric {id} is '{rubric.Status}', not 'Draft' — unlock it before editing criteria.");
        }

        var criteria = request.Select(criterion => new RubricCriterion
        {
            RubricId = rubric.Id,
            Name = criterion.Name,
            Description = criterion.Description,
            MaxScore = criterion.MaxScore,
            OrderIndex = criterion.OrderIndex,
        }).ToList();

        try
        {
            var newCriteria = await repo.UpdateCriteriaAsync(rubric, criteria, cancellationToken);
            return Results.Ok(newCriteria);
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }

    private static async Task<IResult> ConfirmRubricAsync(
        Guid id,
        ClaimsPrincipal user,
        IRubricRepository repo,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, repo, includeCriteria: true, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        try
        {
            rubric!.Confirm();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }

        try
        {
            rubric = await repo.ConfirmAsync(rubric, cancellationToken);
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }

        await eventBus.PublishAsync(
            new RubricConfirmed(
                rubric.Id,
                rubric.SubjectId,
                rubric.AssignmentId,
                rubric.Scope.ToString(),
                rubric.Criteria
                    .Select(c => new RubricConfirmedCriterion(c.Id, c.Name, c.Description, c.MaxScore, c.OrderIndex))
                    .ToList()),
            cancellationToken);

        return Results.Ok(rubric);
    }

    private static async Task<IResult> UnlockRubricAsync(
        Guid id,
        ClaimsPrincipal user,
        IRubricRepository repo,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, repo, includeCriteria: false, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        try
        {
            rubric!.Unlock();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }

        try
        {
            rubric = await repo.UnlockAsync(rubric, cancellationToken);
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }

        return Results.Ok(rubric);
    }

    /// <summary>Loads a rubric by id, translating "not found" and "not authorized" into the matching <see cref="IResult"/>;
    /// callers get back a non-null <c>Rubric</c> when <c>Error</c> is null.</summary>
    private static async Task<(Rubric? Rubric, IResult? Error)> LoadAuthorizedRubricAsync(
        Guid id,
        ClaimsPrincipal user,
        IRubricRepository repo,
        bool includeCriteria,
        CancellationToken cancellationToken)
    {
        var rubric = await repo.GetByIdAsync(id, includeCriteria, cancellationToken);
        if (rubric is null)
        {
            return (null, Results.NotFound());
        }

        return IsAuthorized(rubric, user) ? (rubric, null) : (null, Results.Forbid());
    }

    /// <summary>A caller may act on a rubric if they're an admin, or the owning lecturer for a `Lecturer`-scoped rubric.
    /// `SchoolWide` rubrics have no owning lecturer, so only admins may edit/confirm/unlock them.</summary>
    private static bool IsAuthorized(Rubric rubric, ClaimsPrincipal user) =>
        user.IsInRole("admin") || rubric.LecturerId == user.GetUserId();

    /// <summary>A caller may read a rubric if it's already `Confirmed` (the point where students are
    /// meant to see grading criteria), or if they're otherwise authorized to edit it — Draft/Parsing
    /// rubrics are a lecturer's in-progress work and shouldn't leak to other lecturers or students.</summary>
    private static bool CanView(Rubric rubric, ClaimsPrincipal user) =>
        rubric.Status == RubricStatus.Confirmed || IsAuthorized(rubric, user);
}

public sealed class UploadRubricForm
{
    public Guid SubjectId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
    public RubricScope Scope { get; set; } = RubricScope.Lecturer;
}

public sealed record UpdateCriterionRequest(string Name, string? Description, decimal MaxScore, int OrderIndex);
