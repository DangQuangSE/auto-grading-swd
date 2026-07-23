using AutoGrading.Grading.Api.Constant;
using AutoGrading.Grading.Api.Domain;
using AutoGrading.Grading.Api.Interfaces;
using AutoGrading.Grading.Api.Jobs;
using Hangfire;

namespace AutoGrading.Grading.Api.Service;

public sealed class GradingService(
    IGradingRepository repository,
    ISubmissionApiClient submissions,
    ICatalogApiClient catalog,
    IBackgroundJobClient backgroundJobs) : IGradingService
{
    public async Task<IReadOnlyList<AiGradingRun>> GetRunsForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct)
    {
        if (requester.IsLecturer && !await IsLecturerAllowedAsync(submissionId, requester.UserId, ct))
            throw new GradingForbiddenException();

        return await repository.GetRunsForSubmissionAsync(submissionId, ct);
    }

    public async Task<PublishedResultData> GetPublishedResultForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct)
    {
        if (!await CanReadSubmissionAsync(submissionId, requester, ct))
            throw new GradingForbiddenException();

        var publication = await repository.GetLatestPublicationAsync(submissionId, ct);
        if (publication is null)
        {
            // Never leak raw AI scores to students before a lecturer publishes.
            // Tell the client whether grading is done so it can show an appropriate banner.
            var gradingDone = await repository.HasCompletedRunAsync(submissionId, ct);
            return new PublishedResultData(null, null, null, gradingDone);
        }

        var finalGrade = await repository.GetFinalGradeByIdAsync(publication.FinalGradeId, ct);
        if (finalGrade is null) return new PublishedResultData(null, null, null, null);

        AiGradingRun? run = null;
        if (finalGrade.GradingRunId is Guid runId)
            run = await repository.GetRunByIdAsync(runId, submissionId, ct);

        return new PublishedResultData(finalGrade, publication.PublishedAt, run, true);
    }

    public async Task<FinalGrade?> GetFinalGradeForRequesterAsync(Guid submissionId, RequesterContext requester, CancellationToken ct)
    {
        if (!await CanReadSubmissionAsync(submissionId, requester, ct))
            throw new GradingForbiddenException();

        return await repository.GetLatestFinalGradeAsync(submissionId, ct);
    }

    public async Task<IReadOnlyList<FinalGradeData>> GetFinalGradesBatchForRequesterAsync(IReadOnlyCollection<Guid> submissionIds, RequesterContext requester, CancellationToken ct)
    {
        var ids = submissionIds;
        if (requester.IsLecturer)
        {
            ids = await FilterAllowedForLecturerAsync([.. submissionIds], requester.UserId, ct);
            if (ids.Count == 0) return [];
        }

        var grades = await repository.GetLatestFinalGradesBatchAsync(ids, ct);
        return grades.Select(f => new FinalGradeData(f.SubmissionId, f.Id, f.FinalScore, f.CreatedAt)).ToList();
    }

    public async Task RegradeAsync(Guid submissionId, string? assignmentDescriptionOverride, RequesterContext requester, CancellationToken ct)
    {
        if (requester.IsLecturer && !await IsLecturerAllowedAsync(submissionId, requester.UserId, ct))
            throw new GradingForbiddenException();

        backgroundJobs.Enqueue<AiGradingJob>(job => job.ExecuteAsync(submissionId, assignmentDescriptionOverride, CancellationToken.None));
    }

    public async Task<FinalGrade> PublishGradeAsync(Guid submissionId, Guid? gradingRunId, decimal finalScore, string? notes, RequesterContext requester, CancellationToken ct)
    {
        if (requester.IsLecturer && !await IsLecturerAllowedAsync(submissionId, requester.UserId, ct))
            throw new GradingForbiddenException();

        // Idempotency: if a publication already exists for this submission, return it unchanged
        // rather than creating a duplicate — load-bearing for PublishAllAsync's batch loop below.
        var existing = await repository.GetLatestFinalGradeAsync(submissionId, ct);
        if (existing is not null) return existing;

        if (gradingRunId is Guid runId && !await repository.IsRunCompletedAsync(runId, submissionId, ct))
            throw new InvalidGradingRunException(GradingConstants.GradingRunNotCompleted);

        return await repository.PublishAsync(submissionId, gradingRunId, finalScore, notes, requester.UserId!.Value, ct);
    }

    public async Task<PublishAllResult> PublishAllAsync(RequesterContext requester, CancellationToken ct)
    {
        if (!requester.IsAdmin) throw new GradingForbiddenException();

        const int batchSize = 100;
        var skipped = await repository.CountPublicationsAsync(ct);
        var published = 0;
        var failed = 0;

        while (true)
        {
            var runs = await repository.GetUnpublishedCompletedRunsBatchAsync(batchSize, ct);
            if (runs.Count == 0) break;

            foreach (var run in runs)
            {
                try
                {
                    await repository.PublishAsync(run.SubmissionId, run.Id, run.Scores.Sum(s => s.SuggestedScore), null, requester.UserId!.Value, ct);
                    published++;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    failed++;
                }
            }
            if (runs.Count < batchSize || failed > 0) break;
        }

        return new PublishAllResult(published, skipped, failed);
    }

    private async Task<bool> IsLecturerAllowedAsync(Guid submissionId, Guid? lecturerId, CancellationToken ct)
    {
        if (lecturerId is null) return false;
        var submission = await submissions.GetSubmissionAsync(submissionId, ct);
        if (submission is null) return false;
        var assignment = await catalog.GetAssignmentAsync(submission.AssignmentId, ct);
        if (assignment is null) return false;
        var allowedStudentIds = await catalog.GetLecturerStudentIdsAsync(lecturerId.Value, assignment.SubjectId, ct);
        return allowedStudentIds.Contains(submission.StudentId);
    }

    private async Task<bool> CanReadSubmissionAsync(Guid submissionId, RequesterContext requester, CancellationToken ct)
    {
        if (requester.IsAdmin) return true;
        if (requester.IsLecturer) return await IsLecturerAllowedAsync(submissionId, requester.UserId, ct);
        if (requester.UserId is null) return false;
        var submission = await submissions.GetSubmissionAsync(submissionId, ct);
        return submission?.StudentId == requester.UserId;
    }

    /// <summary>Narrows a batch of submission ids down to the ones the calling lecturer may see,
    /// caching the allowed-student-id lookup per assignment since a batch commonly spans one
    /// assignment's worth of submissions.</summary>
    private async Task<HashSet<Guid>> FilterAllowedForLecturerAsync(HashSet<Guid> submissionIds, Guid? lecturerId, CancellationToken ct)
    {
        if (lecturerId is null) return [];

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
                    : await catalog.GetLecturerStudentIdsAsync(lecturerId.Value, assignment.SubjectId, ct);
                allowedStudentIdsByAssignment[submission.AssignmentId] = allowedStudentIds;
            }

            if (allowedStudentIds.Contains(submission.StudentId))
            {
                allowed.Add(submissionId);
            }
        }

        return allowed;
    }
}
