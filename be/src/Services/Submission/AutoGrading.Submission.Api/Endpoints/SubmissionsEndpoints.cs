using AutoGrading.Common.Messaging;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
using AutoGrading.SubmissionSvc.Api.Data;
using AutoGrading.SubmissionSvc.Api.Domain;
using AutoGrading.SubmissionSvc.Api.Jobs;
using AutoGrading.SubmissionSvc.Api.Clients;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace AutoGrading.SubmissionSvc.Api.Endpoints;

public static class SubmissionsEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/submissions").WithTags("Submissions");

        group.MapGet("/", async (Guid? assignmentId, Guid? studentId, ClaimsPrincipal user, SubmissionDbContext db, CancellationToken ct) =>
            {
                var query = db.Submissions.AsNoTracking().AsQueryable();
                if (assignmentId is not null)
                {
                    query = query.Where(s => s.AssignmentId == assignmentId);
                }

                if (user.IsInRole("student"))
                {
                    if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var currentStudentId)) return Results.Forbid();
                    query = query.Where(s => s.StudentId == currentStudentId);
                }
                else if (studentId is not null)
                {
                    query = query.Where(s => s.StudentId == studentId);
                }

                return Results.Ok(await query.ToListAsync(ct));
            })
            .RequireAuthorization(policy => policy.RequireRole("student", "lecturer", "admin"));

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, SubmissionDbContext db, CancellationToken ct) =>
            {
                var submission = await db.Submissions.AsNoTracking()
                    .Include(s => s.Artifacts)
                    .FirstOrDefaultAsync(s => s.Id == id, ct);

                if (submission is null) return Results.NotFound();
                if (user.IsInRole("student") && submission.StudentId.ToString() != user.FindFirstValue(ClaimTypes.NameIdentifier)) return Results.Forbid();
                return Results.Ok(submission);
            })
            .RequireAuthorization(policy => policy.RequireRole("student", "lecturer", "admin", "service"));

        group.MapPost("/upload", UploadSubmissionAsync)
            .RequireAuthorization(policy => policy.RequireRole("student", "lecturer", "admin"))
            .DisableAntiforgery();

        group.MapPost("/{id:guid}/retry", async (Guid id, ClaimsPrincipal user, SubmissionDbContext db, IBackgroundJobClient backgroundJobs, CancellationToken ct) =>
            {
                var submission = await db.Submissions.FirstOrDefaultAsync(s => s.Id == id, ct);
                if (submission is null)
                {
                    return Results.NotFound();
                }
                if (user.IsInRole("student") && submission.StudentId.ToString() != user.FindFirstValue(ClaimTypes.NameIdentifier)) return Results.Forbid();

                // Clean up previous artifacts
                var oldArtifacts = await db.ExtractedArtifacts.Where(a => a.SubmissionId == id).ToListAsync(ct);
                db.ExtractedArtifacts.RemoveRange(oldArtifacts);

                submission.State = SubmissionState.Uploaded;
                submission.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                backgroundJobs.Enqueue<ExtractionJob>(j => j.ExecuteAsync(id, CancellationToken.None));

                return Results.Accepted();
            })
            .RequireAuthorization(policy => policy.RequireRole("student", "lecturer", "admin"));

        return app;
    }

    private static async Task<IResult> UploadSubmissionAsync(
        [FromForm] UploadSubmissionForm form,
        ClaimsPrincipal user,
        SubmissionDbContext db,
        ICatalogApiClient catalog,
        IObjectStorage storage,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var assignment = await catalog.GetAssignmentAsync(form.AssignmentId, cancellationToken);
        if (assignment is null) return Results.NotFound(new { error = "Assignment not found." });

        Guid studentId;
        if (user.IsInRole("student"))
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out studentId)) return Results.Forbid();
        }
        else if (form.StudentId is Guid requestedStudentId) studentId = requestedStudentId;
        else return Results.BadRequest(new { error = "StudentId is required for lecturer/admin uploads." });

        Submission submission;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var used = await db.Submissions.CountAsync(s => s.AssignmentId == form.AssignmentId && s.StudentId == studentId, cancellationToken);
            if (used >= assignment.MaxAttempts)
                return Results.Conflict(new { error = "Submission attempt limit reached.", usedAttempts = used, maxAttempts = assignment.MaxAttempts });
            var lastAttempt = await db.Submissions
                .Where(s => s.AssignmentId == form.AssignmentId && s.StudentId == studentId)
                .Select(s => (int?)s.AttemptNumber).MaxAsync(cancellationToken) ?? 0;
            submission = new Submission { AssignmentId = form.AssignmentId, StudentId = studentId, AttemptNumber = lastAttempt + 1, State = SubmissionState.Uploading };
            db.Submissions.Add(submission);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var used = await db.Submissions.CountAsync(s => s.AssignmentId == form.AssignmentId && s.StudentId == studentId, cancellationToken);
            return Results.Conflict(new { error = "Submission attempt conflict. Please refresh and try again.", usedAttempts = used, maxAttempts = assignment.MaxAttempts });
        }

        var reportKey = $"submissions/{Guid.NewGuid()}-{form.ReportFile.FileName}";
        string? diagramKey = null;
        try
        {
            await using (var stream = form.ReportFile.OpenReadStream())
            {
                await storage.UploadAsync(reportKey, stream, form.ReportFile.ContentType, cancellationToken);
            }

            if (form.DiagramFile is not null)
            {
                diagramKey = $"submissions/{Guid.NewGuid()}-{form.DiagramFile.FileName}";
                await using var stream = form.DiagramFile.OpenReadStream();
                await storage.UploadAsync(diagramKey, stream, form.DiagramFile.ContentType, cancellationToken);
            }

            submission.ReportObjectKey = reportKey;
            submission.DiagramObjectKey = diagramKey;
            submission.State = SubmissionState.Uploaded;
            submission.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            db.Submissions.Remove(submission);
            await db.SaveChangesAsync(CancellationToken.None);
            try { await storage.DeleteAsync(reportKey, CancellationToken.None); } catch { }
            if (diagramKey is not null) try { await storage.DeleteAsync(diagramKey, CancellationToken.None); } catch { }
            throw;
        }

        await eventBus.PublishAsync(
            new SubmissionUploaded(submission.Id, submission.AssignmentId, submission.StudentId, reportKey, diagramKey),
            cancellationToken);

        await eventBus.PublishAsync(
            new SubmissionStatusChanged(submission.Id, submission.StudentId, "Uploaded"),
            cancellationToken);

        return Results.Created($"/submissions/{submission.Id}", submission);
    }
}

public sealed class UploadSubmissionForm
{
    public Guid AssignmentId { get; set; }
    public Guid? StudentId { get; set; }
    public IFormFile ReportFile { get; set; } = null!;
    public IFormFile? DiagramFile { get; set; }
}
