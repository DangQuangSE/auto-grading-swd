using System.Security.Claims;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Clients;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Domain;
using AutoGrading.Grading.Api.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Grading.Api.Endpoints;

public static class GradesEndpoints
{
    public static IEndpointRouteBuilder MapGradesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/grades").WithTags("Grades");

        // AI output is review material. It is never exposed directly to students.
        group.MapGet("/{submissionId:guid}/runs", GetRunsAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        // Student-safe projection: only the run selected by a publication is returned.
        group.MapGet("/{submissionId:guid}/result", GetPublishedResultAsync)
            .RequireAuthorization();

        group.MapGet("/{submissionId:guid}/final", GetFinalGradeAsync)
            .RequireAuthorization();

        group.MapGet("/final", GetFinalGradesBatchAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/{submissionId:guid}/publish", PublishGradeAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/publish-all", PublishAllAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));

        group.MapPost("/{submissionId:guid}/regrade", RegradeAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }

    private static async Task<IResult> GetRunsAsync(
        Guid submissionId, ClaimsPrincipal user, GradingDbContext db,
        ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        if (user.IsInRole("lecturer") && !await IsLecturerAllowedAsync(submissionId, user, submissions, catalog, ct))
            return Results.Forbid();

        return Results.Ok(await db.AiGradingRuns.AsNoTracking().Include(r => r.Scores)
            .Where(r => r.SubmissionId == submissionId).ToListAsync(ct));
    }

    private static async Task<IResult> GetPublishedResultAsync(
        Guid submissionId, ClaimsPrincipal user, GradingDbContext db,
        ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        if (!await CanReadSubmissionAsync(submissionId, user, submissions, catalog, ct))
            return Results.Forbid();

        var publication = await db.GradePublications.AsNoTracking()
            .Where(p => p.SubmissionId == submissionId)
            .OrderByDescending(p => p.PublishedAt)
            .FirstOrDefaultAsync(ct);
        if (publication is null) return Results.NotFound();

        var finalGrade = await db.FinalGrades.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == publication.FinalGradeId, ct);
        if (finalGrade is null) return Results.NotFound();

        AiGradingRun? run = null;
        if (finalGrade.GradingRunId is Guid runId)
            run = await db.AiGradingRuns.AsNoTracking().Include(r => r.Scores)
                .FirstOrDefaultAsync(r => r.Id == runId && r.SubmissionId == submissionId, ct);

        return Results.Ok(new PublishedGradeResult(finalGrade, publication.PublishedAt, run));
    }

    private static async Task<IResult> GetFinalGradeAsync(
        Guid submissionId, ClaimsPrincipal user, GradingDbContext db,
        ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        if (!await CanReadSubmissionAsync(submissionId, user, submissions, catalog, ct))
            return Results.Forbid();

        var finalGrade = await db.FinalGrades.AsNoTracking()
            .Join(db.GradePublications.AsNoTracking(), f => f.Id, p => p.FinalGradeId, (f, p) => new { Grade = f, p.PublishedAt })
            .Where(x => x.Grade.SubmissionId == submissionId)
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => x.Grade)
            .FirstOrDefaultAsync(ct);
        return finalGrade is null ? Results.NotFound() : Results.Ok(finalGrade);
    }

    private static async Task<bool> CanReadSubmissionAsync(
        Guid submissionId, ClaimsPrincipal user, ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        if (user.IsInRole("admin")) return true;
        if (user.IsInRole("lecturer")) return await IsLecturerAllowedAsync(submissionId, user, submissions, catalog, ct);
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return false;
        var submission = await submissions.GetSubmissionAsync(submissionId, ct);
        return submission?.StudentId == userId;
    }

    /// <summary>True if the calling lecturer teaches a class the submission's student is
    /// enrolled in, for that submission's assignment's subject.</summary>
    private static async Task<bool> IsLecturerAllowedAsync(
        Guid submissionId, ClaimsPrincipal user, ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var lecturerId)) return false;
        var submission = await submissions.GetSubmissionAsync(submissionId, ct);
        if (submission is null) return false;
        var assignment = await catalog.GetAssignmentAsync(submission.AssignmentId, ct);
        if (assignment is null) return false;
        var allowedStudentIds = await catalog.GetLecturerStudentIdsAsync(lecturerId, assignment.SubjectId, ct);
        return allowedStudentIds.Contains(submission.StudentId);
    }

    private static async Task<IResult> GetFinalGradesBatchAsync(
        string[]? submissionIds, ClaimsPrincipal user, GradingDbContext db,
        ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        var ids = ParseIds(submissionIds);
        if (ids is null) return Results.Ok(Array.Empty<FinalGradeResponse>());

        if (user.IsInRole("lecturer"))
        {
            ids = await FilterAllowedForLecturerAsync(ids, user, submissions, catalog, ct);
            if (ids.Count == 0) return Results.Ok(Array.Empty<FinalGradeResponse>());
        }

        var grades = await db.FinalGrades.AsNoTracking()
            .Join(db.GradePublications.AsNoTracking(), f => f.Id, p => p.FinalGradeId, (f, p) => new { Grade = f, p.PublishedAt })
            .Where(x => ids.Contains(x.Grade.SubmissionId))
            .OrderByDescending(x => x.PublishedAt).ToListAsync(ct);
        return Results.Ok(grades.GroupBy(x => x.Grade.SubmissionId).Select(g => g.First().Grade)
            .Select(f => new FinalGradeResponse(f.SubmissionId, f.Id, f.FinalScore, f.CreatedAt)));
    }

    /// <summary>Narrows a batch of submission ids down to the ones the calling lecturer may see,
    /// caching the allowed-student-id lookup per assignment since a batch commonly spans one
    /// assignment's worth of submissions.</summary>
    private static async Task<HashSet<Guid>> FilterAllowedForLecturerAsync(
        HashSet<Guid> submissionIds, ClaimsPrincipal user, ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var lecturerId)) return [];

        var allowed = new HashSet<Guid>();
        var allowedStudentIdsByAssignment = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var submissionId in submissionIds)
        {
            var submission = await submissions.GetSubmissionAsync(submissionId, ct);
            if (submission is null) continue;

            if (!allowedStudentIdsByAssignment.TryGetValue(submission.AssignmentId, out var allowedStudentIds))
            {
                var assignment = await catalog.GetAssignmentAsync(submission.AssignmentId, ct);
                allowedStudentIds = assignment is null
                    ? []
                    : await catalog.GetLecturerStudentIdsAsync(lecturerId, assignment.SubjectId, ct);
                allowedStudentIdsByAssignment[submission.AssignmentId] = allowedStudentIds;
            }

            if (allowedStudentIds.Contains(submission.StudentId))
            {
                allowed.Add(submissionId);
            }
        }

        return allowed;
    }

    private static HashSet<Guid>? ParseIds(string[]? ids)
    {
        if (ids is not { Length: > 0 }) return null;
        var parsed = ids.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(v => Guid.TryParse(v, out _)).Select(Guid.Parse).ToHashSet();
        return parsed.Count > 0 ? parsed : null;
    }

    private static async Task<IResult> RegradeAsync(
        Guid submissionId, RegradeRequest request, ClaimsPrincipal user,
        ISubmissionApiClient submissions, ICatalogApiClient catalog, IBackgroundJobClient jobs, CancellationToken ct)
    {
        if (user.IsInRole("lecturer") && !await IsLecturerAllowedAsync(submissionId, user, submissions, catalog, ct))
            return Results.Forbid();

        jobs.Enqueue<AiGradingJob>(job => job.ExecuteAsync(submissionId, request.AssignmentDescription, CancellationToken.None));
        return Results.Accepted($"/grades/{submissionId}/runs");
    }

    private static async Task<IResult> PublishGradeAsync(
        Guid submissionId, PublishGradeRequest request, ClaimsPrincipal user,
        GradingDbContext db, ISubmissionApiClient submissions, ICatalogApiClient catalog, CancellationToken ct)
    {
        if (user.IsInRole("lecturer") && !await IsLecturerAllowedAsync(submissionId, user, submissions, catalog, ct))
            return Results.Forbid();

        var existing = await FindPublishedGradeAsync(submissionId, db, ct);
        if (existing is not null) return Results.Ok(existing);

        if (request.GradingRunId is Guid runId && !await db.AiGradingRuns.AnyAsync(
                r => r.Id == runId && r.SubmissionId == submissionId && r.Status == AiGradingRunStatus.Completed, ct))
            return Results.BadRequest(new { error = "The grading run is not a completed run for this submission." });

        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var grade = await PublishOneAsync(submissionId, request.GradingRunId, request.FinalScore, request.Notes, userId, db, ct);
        return Results.Created($"/grades/{submissionId}/final", grade);
    }

    private static async Task<IResult> PublishAllAsync(
        ClaimsPrincipal user, GradingDbContext db, CancellationToken ct)
    {
        const int batchSize = 100;
        var skipped = await db.GradePublications.AsNoTracking().CountAsync(ct);
        var published = 0;
        var failed = 0;
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        while (true)
        {
            var runIds = await db.AiGradingRuns.AsNoTracking()
                .Where(r => r.Status == AiGradingRunStatus.Completed && r.Scores.Any() &&
                            !db.GradePublications.Any(p => p.SubmissionId == r.SubmissionId))
                .GroupBy(r => r.SubmissionId)
                .Select(g => g.OrderByDescending(r => r.CompletedAt).ThenByDescending(r => r.CreatedAt).Select(r => r.Id).First())
                .Take(batchSize).ToListAsync(ct);
            if (runIds.Count == 0) break;

            var runs = await db.AiGradingRuns.AsNoTracking().Include(r => r.Scores)
                .Where(r => runIds.Contains(r.Id)).ToListAsync(ct);
            foreach (var run in runs)
            {
                try
                {
                    await PublishOneAsync(run.SubmissionId, run.Id, run.Scores.Sum(s => s.SuggestedScore), null, userId, db, ct);
                    published++;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    failed++;
                    db.ChangeTracker.Clear();
                }
            }
            if (runs.Count < batchSize || failed > 0) break;
        }

        return Results.Ok(new PublishAllResponse(published, skipped, failed));
    }

    private static async Task<FinalGrade?> FindPublishedGradeAsync(Guid submissionId, GradingDbContext db, CancellationToken ct) =>
        await db.FinalGrades.AsNoTracking()
            .Join(db.GradePublications.AsNoTracking(), f => f.Id, p => p.FinalGradeId, (f, p) => new { f, p.PublishedAt })
            .Where(x => x.f.SubmissionId == submissionId).OrderByDescending(x => x.PublishedAt).Select(x => x.f)
            .FirstOrDefaultAsync(ct);

    private static async Task<FinalGrade> PublishOneAsync(
        Guid submissionId, Guid? runId, decimal score, string? notes, Guid userId,
        GradingDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var grade = new FinalGrade { SubmissionId = submissionId, GradingRunId = runId, FinalScore = score, Notes = notes, CreatedByUserId = userId };
        db.FinalGrades.Add(grade);
        db.GradePublications.Add(new GradePublication { FinalGradeId = grade.Id, SubmissionId = submissionId, PublishedByUserId = userId });
        db.GradePublishedOutbox.Add(new GradePublishedOutbox { SubmissionId = submissionId, FinalGradeId = grade.Id, FinalScore = grade.FinalScore, PublishedByUserId = userId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return grade;
    }
}

public sealed record RegradeRequest(string? AssignmentDescription);
public sealed record PublishGradeRequest(Guid? GradingRunId, decimal FinalScore, string? Notes);
public sealed record FinalGradeResponse(Guid SubmissionId, Guid FinalGradeId, decimal FinalScore, DateTimeOffset CreatedAt);
public sealed record PublishedGradeResult(FinalGrade FinalGrade, DateTimeOffset PublishedAt, AiGradingRun? GradingRun);
public sealed record PublishAllResponse(int Published, int Skipped, int Failed);
