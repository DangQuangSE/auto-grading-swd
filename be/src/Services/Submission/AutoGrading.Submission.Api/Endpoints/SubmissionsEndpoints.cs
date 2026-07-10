using AutoGrading.Common.Messaging;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
using AutoGrading.SubmissionSvc.Api.Data;
using AutoGrading.SubmissionSvc.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.SubmissionSvc.Api.Endpoints;

public static class SubmissionsEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/submissions").WithTags("Submissions");

        group.MapGet("/", async (Guid? assignmentId, Guid? studentId, SubmissionDbContext db, CancellationToken ct) =>
            {
                var query = db.Submissions.AsNoTracking().AsQueryable();
                if (assignmentId is not null)
                {
                    query = query.Where(s => s.AssignmentId == assignmentId);
                }

                if (studentId is not null)
                {
                    query = query.Where(s => s.StudentId == studentId);
                }

                return Results.Ok(await query.ToListAsync(ct));
            })
            .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, SubmissionDbContext db, CancellationToken ct) =>
            {
                var submission = await db.Submissions.AsNoTracking()
                    .Include(s => s.Artifacts)
                    .FirstOrDefaultAsync(s => s.Id == id, ct);

                return submission is null ? Results.NotFound() : Results.Ok(submission);
            })
            .RequireAuthorization();

        group.MapPost("/upload", UploadSubmissionAsync)
            .RequireAuthorization(policy => policy.RequireRole("student", "lecturer", "admin"))
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> UploadSubmissionAsync(
        [FromForm] UploadSubmissionForm form,
        SubmissionDbContext db,
        IObjectStorage storage,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var reportKey = $"submissions/{Guid.NewGuid()}-{form.ReportFile.FileName}";
        await using (var stream = form.ReportFile.OpenReadStream())
        {
            await storage.UploadAsync(reportKey, stream, form.ReportFile.ContentType, cancellationToken);
        }

        var diagramKey = $"submissions/{Guid.NewGuid()}-{form.DiagramFile.FileName}";
        await using (var stream = form.DiagramFile.OpenReadStream())
        {
            await storage.UploadAsync(diagramKey, stream, form.DiagramFile.ContentType, cancellationToken);
        }

        var submission = new Submission
        {
            AssignmentId = form.AssignmentId,
            StudentId = form.StudentId,
            ReportObjectKey = reportKey,
            DiagramObjectKey = diagramKey,
            State = SubmissionState.Uploaded,
        };

        db.Submissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new SubmissionUploaded(submission.Id, submission.AssignmentId, submission.StudentId, reportKey, diagramKey),
            cancellationToken);

        return Results.Created($"/submissions/{submission.Id}", submission);
    }
}

public sealed class UploadSubmissionForm
{
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public IFormFile ReportFile { get; set; } = null!;
    public IFormFile DiagramFile { get; set; } = null!;
}
