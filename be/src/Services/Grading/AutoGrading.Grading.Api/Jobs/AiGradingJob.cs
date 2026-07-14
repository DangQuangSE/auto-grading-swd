using System.Text.Json;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Clients;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Domain;
using AutoGrading.Common.OpenCode;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Grading.Api.Jobs;

/// <summary>
/// Hangfire background job: runs AI grading for a submission whose artifacts have been extracted,
/// fetching the real lecturer-defined rubric criteria from Catalog and the real extracted
/// report/diagram content from Submission before calling the LLM.
/// </summary>
public sealed class AiGradingJob(
    GradingDbContext db,
    ICatalogApiClient catalogApiClient,
    ISubmissionApiClient submissionApiClient,
    IOpenCodeClient openCodeClient,
    OpenCodeOptions openCodeOptions,
    IEventBus eventBus)
{
    public async Task ExecuteAsync(Guid submissionId, string? assignmentDescriptionOverride = null, CancellationToken cancellationToken = default)
    {
        var run = new AiGradingRun
        {
            SubmissionId = submissionId,
            Model = openCodeOptions.Model,
            Status = AiGradingRunStatus.Running,
        };

        db.AiGradingRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var submission = await submissionApiClient.GetSubmissionAsync(submissionId, cancellationToken)
                ?? throw new InvalidOperationException($"Submission {submissionId} was not found.");

            var rubricCriteria = await catalogApiClient.GetCriteriaForAssignmentAsync(submission.AssignmentId, cancellationToken);

            // Fall back to a single general criterion when no rubric has been uploaded yet
            // so grading produces a result rather than failing hard.
            var criteria = rubricCriteria.Count > 0
                ? rubricCriteria.Select(c => new GradingCriterionInput(c.Id, c.Name, c.MaxScore)).ToArray()
                : [new GradingCriterionInput(Guid.NewGuid(), "Overall Quality", 10m)];

            var assignment = await catalogApiClient.GetAssignmentAsync(submission.AssignmentId, cancellationToken);
            var assignmentDescription = assignmentDescriptionOverride ?? assignment?.Description;

            var reportArtifact = submission.Artifacts.FirstOrDefault(a => a.Kind == ArtifactKindDto.Report);
            var diagramArtifact = submission.Artifacts.FirstOrDefault(a => a.Kind == ArtifactKindDto.Diagram);

            var reportContent = reportArtifact?.Content ?? string.Empty;
            var diagramContent = diagramArtifact?.Content ?? string.Empty;
            var images = ParseImages(reportArtifact?.ImagesJson).Concat(ParseImages(diagramArtifact?.ImagesJson)).ToArray();

            var results = await openCodeClient.GradeAsync(
                reportContent: reportContent,
                diagramContent: diagramContent,
                criteria: criteria,
                assignmentDescription: assignmentDescription,
                images: images,
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

    private static string[] ParseImages(string? imagesJson)
    {
        if (string.IsNullOrWhiteSpace(imagesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(imagesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
