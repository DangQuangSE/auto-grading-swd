using System.Security.Claims;
using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Jobs;
using AutoGrading.Common.Storage;
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
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = user.IsInRole("admin");

        if (form.Scope == RubricScope.SchoolWide && !isAdmin)
        {
            return Results.Forbid();
        }

        var existingRubric = form.AssignmentId is null
            ? null
            : await db.Rubrics.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.AssignmentId == form.AssignmentId, cancellationToken);

        if (existingRubric is not null && !isAdmin && existingRubric.LecturerId != userId)
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
            existingRubric.Criteria.Clear();
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
        var rubric = await db.Rubrics.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rubric is null)
        {
            return Results.NotFound();
        }

        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = user.IsInRole("admin");
        if (!isAdmin && rubric.LecturerId != userId)
        {
            return Results.Forbid();
        }

        if (rubric.Status != RubricStatus.Parsing)
        {
            return Results.Conflict($"Rubric {id} is '{rubric.Status}', not 'Parsing' — re-upload instead of retrying.");
        }

        backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(rubric.Id, CancellationToken.None));

        return Results.Accepted();
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
