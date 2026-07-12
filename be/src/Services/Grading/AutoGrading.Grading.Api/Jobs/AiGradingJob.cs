using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Domain;
using AutoGrading.Common.OpenRouter;

namespace AutoGrading.Grading.Api.Jobs;

/// <summary>
/// Hangfire background job: runs AI grading for a submission whose artifacts have been
/// extracted. Rubric criteria are not yet fetched cross-service (deferred to a later sprint),
/// so a single placeholder "Overall Quality" criterion is used until Catalog integration lands.
/// </summary>
public sealed class AiGradingJob(
    GradingDbContext db,
    IOpenRouterClient openRouterClient,
    OpenRouterOptions openRouterOptions,
    IEventBus eventBus)
{
    public async Task ExecuteAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var run = new AiGradingRun
        {
            SubmissionId = submissionId,
            Model = openRouterOptions.Model,
            Status = AiGradingRunStatus.Running,
        };

        db.AiGradingRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var criteria = new[]
        {
            new GradingCriterionInput(Guid.NewGuid(), "Overall Quality", 100m),
        };

        try
        {
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
