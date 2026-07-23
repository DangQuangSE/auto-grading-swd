using AutoGrading.Grading.Api.Domain;
using AutoGrading.Grading.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Grading.Api.Repository;

public sealed class GradingRepository(GradingDbContext db) : IGradingRepository
{
    public async Task<IReadOnlyList<AiGradingRun>> GetRunsForSubmissionAsync(Guid submissionId, CancellationToken ct) =>
        await db.AiGradingRuns.AsNoTracking().Include(r => r.Scores)
            .Where(r => r.SubmissionId == submissionId).ToListAsync(ct);

    public async Task<GradePublication?> GetLatestPublicationAsync(Guid submissionId, CancellationToken ct) =>
        await db.GradePublications.AsNoTracking()
            .Where(p => p.SubmissionId == submissionId)
            .OrderByDescending(p => p.PublishedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<FinalGrade?> GetFinalGradeByIdAsync(Guid finalGradeId, CancellationToken ct) =>
        await db.FinalGrades.AsNoTracking().FirstOrDefaultAsync(f => f.Id == finalGradeId, ct);

    public async Task<AiGradingRun?> GetRunByIdAsync(Guid runId, Guid submissionId, CancellationToken ct) =>
        await db.AiGradingRuns.AsNoTracking().Include(r => r.Scores)
            .FirstOrDefaultAsync(r => r.Id == runId && r.SubmissionId == submissionId, ct);

    public async Task<FinalGrade?> GetLatestFinalGradeAsync(Guid submissionId, CancellationToken ct) =>
        await db.FinalGrades.AsNoTracking()
            .Join(db.GradePublications.AsNoTracking(), f => f.Id, p => p.FinalGradeId, (f, p) => new { Grade = f, p.PublishedAt })
            .Where(x => x.Grade.SubmissionId == submissionId)
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => x.Grade)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<FinalGrade>> GetLatestFinalGradesBatchAsync(IReadOnlyCollection<Guid> submissionIds, CancellationToken ct)
    {
        var grades = await db.FinalGrades.AsNoTracking()
            .Join(db.GradePublications.AsNoTracking(), f => f.Id, p => p.FinalGradeId, (f, p) => new { Grade = f, p.PublishedAt })
            .Where(x => submissionIds.Contains(x.Grade.SubmissionId))
            .OrderByDescending(x => x.PublishedAt).ToListAsync(ct);

        return grades.GroupBy(x => x.Grade.SubmissionId).Select(g => g.First().Grade).ToList();
    }

    public async Task<bool> IsRunCompletedAsync(Guid runId, Guid submissionId, CancellationToken ct) =>
        await db.AiGradingRuns.AnyAsync(r => r.Id == runId && r.SubmissionId == submissionId && r.Status == AiGradingRunStatus.Completed, ct);

    public async Task<bool> HasCompletedRunAsync(Guid submissionId, CancellationToken ct) =>
        await db.AiGradingRuns.AnyAsync(r => r.SubmissionId == submissionId && r.Status == AiGradingRunStatus.Completed, ct);

    public async Task<FinalGrade> PublishAsync(Guid submissionId, Guid? runId, decimal score, string? notes, Guid userId, CancellationToken ct)
    {
        try
        {
            // CRITICAL: this 3-table write must stay entirely inside this method — do not split it across
            // Service/Endpoints. On failure, ChangeTracker.Clear() keeps this DbContext instance safe for
            // PublishAllAsync's next call.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var grade = new FinalGrade { SubmissionId = submissionId, GradingRunId = runId, FinalScore = score, Notes = notes, CreatedByUserId = userId };
            db.FinalGrades.Add(grade);
            db.GradePublications.Add(new GradePublication { FinalGradeId = grade.Id, SubmissionId = submissionId, PublishedByUserId = userId });
            db.GradePublishedOutbox.Add(new GradePublishedOutbox { SubmissionId = submissionId, FinalGradeId = grade.Id, FinalScore = grade.FinalScore, PublishedByUserId = userId });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return grade;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<int> CountPublicationsAsync(CancellationToken ct) =>
        await db.GradePublications.AsNoTracking().CountAsync(ct);

    public async Task<IReadOnlyList<AiGradingRun>> GetUnpublishedCompletedRunsBatchAsync(int batchSize, CancellationToken ct)
    {
        var runIds = await db.AiGradingRuns.AsNoTracking()
            .Where(r => r.Status == AiGradingRunStatus.Completed && r.Scores.Any() &&
                        !db.GradePublications.Any(p => p.SubmissionId == r.SubmissionId))
            .GroupBy(r => r.SubmissionId)
            .Select(g => g.OrderByDescending(r => r.CompletedAt).ThenByDescending(r => r.CreatedAt).Select(r => r.Id).First())
            .Take(batchSize).ToListAsync(ct);

        if (runIds.Count == 0) return [];

        return await db.AiGradingRuns.AsNoTracking().Include(r => r.Scores)
            .Where(r => runIds.Contains(r.Id)).ToListAsync(ct);
    }

    public async Task AddRunAsync(AiGradingRun run, CancellationToken ct)
    {
        db.AiGradingRuns.Add(run);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateRunStatusAsync(Guid runId, AiGradingRunStatus status, DateTimeOffset completedAt, CancellationToken ct)
    {
        var run = await db.AiGradingRuns.FirstAsync(r => r.Id == runId, ct);
        run.Status = status;
        run.CompletedAt = completedAt;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddCriterionScoresAsync(Guid runId, IReadOnlyList<AiCriterionScore> scores, CancellationToken ct)
    {
        db.AiCriterionScores.AddRange(scores);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GradePublishedOutbox>> GetPendingOutboxMessagesAsync(int batchSize, CancellationToken ct) =>
        await db.GradePublishedOutbox.Where(x => x.DispatchedAt == null)
            .OrderBy(x => x.CreatedAt).Take(batchSize).ToListAsync(ct);

    public async Task MarkOutboxDispatchedAsync(Guid outboxId, CancellationToken ct)
    {
        var message = await db.GradePublishedOutbox.FirstAsync(x => x.Id == outboxId, ct);
        message.DispatchedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
