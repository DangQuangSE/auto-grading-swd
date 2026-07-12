using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Common.Messaging;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
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

        return app;
    }

    private static async Task<IResult> UploadRubricAsync(
        [FromForm] UploadRubricForm form,
        CatalogDbContext db,
        IObjectStorage storage,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var objectKey = $"rubrics/{Guid.NewGuid()}-{form.File.FileName}";
        await using (var stream = form.File.OpenReadStream())
        {
            await storage.UploadAsync(objectKey, stream, form.File.ContentType, cancellationToken);
        }

        var rubric = new Rubric
        {
            SubjectId = form.SubjectId,
            AssignmentId = form.AssignmentId,
            Name = form.Name,
            FileObjectKey = objectKey,
        };

        // Stub parse: real .docx/.drawio rubric parsing lands in a later phase.
        // For now we seed a single placeholder criterion so downstream consumers have something to work with.
        rubric.Criteria.Add(new RubricCriterion
        {
            Name = "Overall Quality",
            Description = "Placeholder criterion pending full rubric parsing.",
            MaxScore = 10,
            OrderIndex = 0,
        });

        db.Rubrics.Add(rubric);
        await db.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new RubricParsed(rubric.Id, rubric.SubjectId, rubric.AssignmentId, rubric.Criteria.Count),
            cancellationToken);

        return Results.Created($"/rubrics/{rubric.Id}", rubric);
    }
}

public sealed class UploadRubricForm
{
    public Guid SubjectId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
}
