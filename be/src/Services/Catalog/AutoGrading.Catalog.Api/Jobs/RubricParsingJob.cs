using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Common.Messaging;
using AutoGrading.Common.OpenCode;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Jobs;

/// <summary>
/// Hangfire background job: downloads a rubric's uploaded .docx, extracts its text, calls the AI
/// extraction client to derive draft criteria, and transitions the rubric from Parsing to Draft.
/// </summary>
public sealed class RubricParsingJob(
    CatalogDbContext db,
    IObjectStorage storage,
    IOpenCodeClient openCodeClient,
    IEventBus eventBus,
    ILogger<RubricParsingJob> logger)
{
    public async Task ExecuteAsync(Guid rubricId, CancellationToken cancellationToken = default)
    {
        var rubric = await db.Rubrics.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.Id == rubricId, cancellationToken);
        if (rubric is null)
        {
            logger.LogWarning("RubricParsingJob: rubric {RubricId} no longer exists; skipping.", rubricId);
            return;
        }

        if (rubric.Status != RubricStatus.Parsing)
        {
            logger.LogInformation(
                "RubricParsingJob: rubric {RubricId} is already {Status}; superseded by a later upload/retry, skipping.",
                rubricId,
                rubric.Status);
            return;
        }

        if (string.IsNullOrEmpty(rubric.FileObjectKey))
        {
            logger.LogError("RubricParsingJob: rubric {RubricId} has no file to parse.", rubricId);
            throw new InvalidOperationException($"Rubric {rubricId} has no uploaded file to parse.");
        }

        try
        {
            string documentText;
            await using (var fileStream = await storage.DownloadAsync(rubric.FileObjectKey, cancellationToken))
            {
                documentText = DocxTextExtractor.ExtractText(fileStream);
            }

            var extractedCriteria = await openCodeClient.ParseRubricCriteriaAsync(documentText, cancellationToken);

            var newCriteria = db.ReplaceRubricCriteria(rubric, extractedCriteria.Select(criterion => new RubricCriterion
            {
                RubricId = rubric.Id,
                Name = criterion.Name,
                Description = criterion.Description,
                MaxScore = criterion.MaxScore,
                OrderIndex = criterion.Order,
            }).ToList());

            rubric.Status = RubricStatus.Draft;
            await db.SaveChangesAsync(cancellationToken);

            await eventBus.PublishAsync(
                new RubricParsed(rubric.Id, rubric.SubjectId, rubric.AssignmentId, newCriteria.Count),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RubricParsingJob: failed to parse rubric {RubricId}; it remains in Parsing for Hangfire retry.", rubricId);
            throw;
        }
    }
}
