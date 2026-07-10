using AutoGrading.Common.Messaging;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
using AutoGrading.SubmissionSvc.Api.Data;
using AutoGrading.SubmissionSvc.Api.Domain;
using AutoGrading.SubmissionSvc.Api.Parsing;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.SubmissionSvc.Api.Jobs;

/// <summary>
/// Hangfire background job: extracts artifacts from an uploaded submission's report/diagram
/// files, moving it through the Uploaded -> Extracting -> Extracted (or Failed) state machine.
/// </summary>
public sealed class ExtractionJob(
    SubmissionDbContext db,
    IObjectStorage storage,
    IArtifactParser parser,
    IEventBus eventBus)
{
    public async Task ExecuteAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await db.Submissions
            .Include(s => s.Artifacts)
            .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

        if (submission is null)
        {
            return;
        }

        submission.State = SubmissionState.Extracting;
        submission.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var warnings = new List<string>();
        var success = true;

        try
        {
            foreach (var (kind, objectKey) in new[]
                     {
                         (ArtifactKind.Report, submission.ReportObjectKey),
                         (ArtifactKind.Diagram, submission.DiagramObjectKey),
                     })
            {
                await using var stream = await storage.DownloadAsync(objectKey, cancellationToken);
                var parsed = await parser.ParseAsync(stream, objectKey, cancellationToken);

                db.ExtractedArtifacts.Add(new ExtractedArtifact
                {
                    SubmissionId = submission.Id,
                    Kind = kind,
                    Content = parsed.Content,
                    Warnings = parsed.Warnings.Length > 0 ? string.Join("; ", parsed.Warnings) : null,
                });
                warnings.AddRange(parsed.Warnings);
            }
        }
        catch (Exception ex)
        {
            success = false;
            warnings.Add(ex.Message);
        }

        submission.State = success ? SubmissionState.Extracted : SubmissionState.Failed;
        submission.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new ArtifactsExtracted(submission.Id, success, warnings.ToArray()),
            cancellationToken);
    }
}
