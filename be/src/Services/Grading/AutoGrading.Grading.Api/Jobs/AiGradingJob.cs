using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Domain;
using AutoGrading.Common.OpenRouter;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Grading.Api.Jobs;

/// <summary>
/// Hangfire background job: runs AI grading for a submission whose artifacts have been
/// extracted, against the assignment's confirmed rubric criteria (Grading's local copy,
/// populated by RubricConfirmedHandler). Fails/retries if no confirmed criteria exist yet.
/// </summary>
public sealed class AiGradingJob(
    GradingDbContext db,
    IOpenRouterClient openRouterClient,
    OpenRouterOptions openRouterOptions,
    IEventBus eventBus)
{
    public async Task ExecuteAsync(Guid submissionId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var run = new AiGradingRun
        {
            SubmissionId = submissionId,
            Model = openRouterOptions.Model,
            Status = AiGradingRunStatus.Running,
        };

        db.AiGradingRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var localRubric = await db.LocalRubrics
                .Include(r => r.Criteria)
                .FirstOrDefaultAsync(r => r.AssignmentId == assignmentId, cancellationToken);

            if (localRubric is null || localRubric.Criteria.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No confirmed rubric criteria found for assignment {assignmentId}. Confirm the rubric in Catalog first.");
            }

            var criteria = localRubric.Criteria
                .Select(c => new GradingCriterionInput(c.RubricCriterionId, c.Name, c.MaxScore))
                .ToList();

            var results = await openRouterClient.GradeAsync(
                reportContent: string.Empty,
                diagramContent: string.Empty,
                criteria: criteria,
                cancellationToken: cancellationToken);

            foreach (var result in results)
            {
                db.AiCriterionScores.Add(new AiCriterionScore
                {
                    GradingRunId = run.Id,
                    SubmissionId = submissionId,
                    RubricCriterionId = result.RubricCriterionId,
                    MaxScore = result.MaxScore,
                    SuggestedScore = result.SuggestedScore,
                    Deductions = result.Deductions,
                    Evidence = result.Evidence,
                    Comment = result.Comment,
                    Confidence = result.Confidence,
                });
            }

            run.Status = AiGradingRunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var totalScore = results.Sum(r => r.SuggestedScore);
            await eventBus.PublishAsync(
                new AiGradingCompleted(submissionId, run.Id, totalScore),
                cancellationToken);
        }
        catch (Exception)
        {
            run.Status = AiGradingRunStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
