using System.Security.Claims;
using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Jobs;
using AutoGrading.Common.Auth;
using AutoGrading.Common.Messaging;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class RubricsEndpoints
{
    public static IEndpointRouteBuilder MapRubricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rubrics").WithTags("Rubrics");

        group.MapGet("/", async (Guid? subjectId, CatalogDbContext db, CancellationToken ct) =>
            {
                var query = db.Rubrics.AsNoTracking().Include(r => r.Criteria).AsQueryable();
                if (subjectId is not null)
                {
                    query = query.Where(r => r.SubjectId == subjectId);
                }

                return Results.Ok(await query.ToListAsync(ct));
            })
            .RequireAuthorization();

        group.MapGet("/{id:guid}/file", async (Guid id, CatalogDbContext db, IObjectStorage storage, CancellationToken ct) =>
            {
                var rubric = await db.Rubrics.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
                if (rubric?.FileObjectKey is null)
                {
                    return Results.NotFound();
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
        CatalogDbContext db,
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
            : await db.Rubrics.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.AssignmentId == form.AssignmentId, cancellationToken);

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
            db.RubricCriteria.RemoveRange(existingRubric.Criteria);
            rubric = existingRubric;
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
            db.Rubrics.Add(rubric);
        }

        await db.SaveChangesAsync(cancellationToken);

        backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(rubric.Id, CancellationToken.None));

        return Results.Created($"/rubrics/{rubric.Id}", rubric);
    }

    private static async Task<IResult> RetryParsingAsync(
        Guid id,
        ClaimsPrincipal user,
        CatalogDbContext db,
        IBackgroundJobClient backgroundJobs,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, db, includeCriteria: false, cancellationToken);
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
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, db, includeCriteria: true, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (rubric!.Status != RubricStatus.Draft)
        {
            return Results.Conflict($"Rubric {id} is '{rubric.Status}', not 'Draft' — unlock it before editing criteria.");
        }

        var newCriteria = db.ReplaceRubricCriteria(rubric, request.Select(criterion => new RubricCriterion
        {
            RubricId = rubric.Id,
            Name = criterion.Name,
            Description = criterion.Description,
            MaxScore = criterion.MaxScore,
            OrderIndex = criterion.OrderIndex,
        }).ToList());

        var saveError = await TrySaveChangesAsync(db, id, cancellationToken);
        return saveError ?? Results.Ok(newCriteria);
    }

    private static async Task<IResult> ConfirmRubricAsync(
        Guid id,
        ClaimsPrincipal user,
        CatalogDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, db, includeCriteria: true, cancellationToken);
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

        var saveError = await TrySaveChangesAsync(db, id, cancellationToken);
        if (saveError is not null)
        {
            return saveError;
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
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var (rubric, error) = await LoadAuthorizedRubricAsync(id, user, db, includeCriteria: false, cancellationToken);
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

        var saveError = await TrySaveChangesAsync(db, id, cancellationToken);
        return saveError ?? Results.Ok(rubric);
    }

    /// <summary>Loads a rubric by id, translating "not found" and "not authorized" into the matching <see cref="IResult"/>;
    /// callers get back a non-null <c>Rubric</c> when <c>Error</c> is null.</summary>
    private static async Task<(Rubric? Rubric, IResult? Error)> LoadAuthorizedRubricAsync(
        Guid id,
        ClaimsPrincipal user,
        CatalogDbContext db,
        bool includeCriteria,
        CancellationToken cancellationToken)
    {
        IQueryable<Rubric> query = includeCriteria ? db.Rubrics.Include(r => r.Criteria) : db.Rubrics;
        var rubric = await query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
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

    private static async Task<IResult?> TrySaveChangesAsync(CatalogDbContext db, Guid rubricId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict($"Rubric {rubricId} was modified concurrently; reload and try again.");
        }
    }
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
