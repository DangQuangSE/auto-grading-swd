using System.Data;
using AutoGrading.SubmissionSvc.Api.Domain;
using AutoGrading.SubmissionSvc.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.SubmissionSvc.Api.Repository;

public sealed class SubmissionRepository(SubmissionDbContext db) : ISubmissionRepository
{
    public async Task<IReadOnlyList<Submission>> ListAsync(Guid? assignmentId, IReadOnlyCollection<Guid>? restrictToStudentIds, Guid? studentId, CancellationToken ct)
    {
        var query = db.Submissions.AsNoTracking().AsQueryable();

        if (assignmentId is not null)
        {
            query = query.Where(s => s.AssignmentId == assignmentId);
        }

        if (restrictToStudentIds is not null)
        {
            query = query.Where(s => restrictToStudentIds.Contains(s.StudentId));
        }

        if (studentId is not null)
        {
            query = query.Where(s => s.StudentId == studentId);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<Submission?> GetByIdAsync(Guid id, bool includeArtifacts, CancellationToken ct)
    {
        var query = db.Submissions.AsNoTracking();
        return includeArtifacts
            ? await query.Include(s => s.Artifacts).FirstOrDefaultAsync(s => s.Id == id, ct)
            : await query.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Submission> CreateWithAttemptCheckAsync(Guid assignmentId, Guid studentId, int maxAttempts, CancellationToken ct)
    {
        try
        {
            // CRITICAL: the Serializable transaction must stay entirely inside this method — do not split the
            // attempt-check and the insert across Service/Endpoints, that reopens the race condition this isolation level prevents.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var used = await db.Submissions.CountAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId, ct);
            if (used >= maxAttempts)
            {
                throw new SubmissionAttemptLimitReachedException(used, maxAttempts);
            }

            var lastAttempt = await db.Submissions
                .Where(s => s.AssignmentId == assignmentId && s.StudentId == studentId)
                .Select(s => (int?)s.AttemptNumber).MaxAsync(ct) ?? 0;

            var submission = new Submission { AssignmentId = assignmentId, StudentId = studentId, AttemptNumber = lastAttempt + 1, State = SubmissionState.Uploading };
            db.Submissions.Add(submission);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return submission;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var used = await db.Submissions.CountAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId, ct);
            throw new SubmissionAttemptConflictException(used, maxAttempts);
        }
    }

    public async Task SaveUploadResultAsync(Submission submission, string reportObjectKey, string? diagramObjectKey, CancellationToken ct)
    {
        submission.ReportObjectKey = reportObjectKey;
        submission.DiagramObjectKey = diagramObjectKey;
        submission.State = SubmissionState.Uploaded;
        submission.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Submission submission, CancellationToken ct)
    {
        db.Submissions.Remove(submission);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetForRetryAsync(Guid submissionId, CancellationToken ct)
    {
        var oldArtifacts = await db.ExtractedArtifacts.Where(a => a.SubmissionId == submissionId).ToListAsync(ct);
        db.ExtractedArtifacts.RemoveRange(oldArtifacts);

        var submission = await db.Submissions.FirstAsync(s => s.Id == submissionId, ct);
        submission.State = SubmissionState.Uploaded;
        submission.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateStateAsync(Guid submissionId, SubmissionState state, CancellationToken ct)
    {
        var submission = await db.Submissions.FirstAsync(s => s.Id == submissionId, ct);
        submission.State = state;
        submission.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddExtractedArtifactAsync(Guid submissionId, ExtractedArtifact artifact, CancellationToken ct)
    {
        artifact.SubmissionId = submissionId;
        db.ExtractedArtifacts.Add(artifact);
        await db.SaveChangesAsync(ct);
    }
}
