using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Data;
using AutoGrading.Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Handlers;

/// <summary>Consumes SubmissionUploaded and records which student a submission belongs to, keyed by
/// SubmissionId. A submission's StudentId never changes, so redelivery is a no-op.</summary>
public sealed class SubmissionUploadedHandler(IdentityDbContext db, ILogger<SubmissionUploadedHandler> logger)
    : IIntegrationEventHandler<SubmissionUploaded>
{
    public async Task HandleAsync(SubmissionUploaded @event, CancellationToken cancellationToken = default)
    {
        var exists = await db.SubmissionStudents.AnyAsync(s => s.SubmissionId == @event.SubmissionId, cancellationToken);
        if (exists)
        {
            logger.LogDebug("SubmissionUploadedHandler: submission {SubmissionId} already recorded; redelivery.", @event.SubmissionId);
            return;
        }

        db.SubmissionStudents.Add(new SubmissionStudent { SubmissionId = @event.SubmissionId, StudentId = @event.StudentId });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsPrimaryKeyViolation())
        {
            logger.LogDebug("SubmissionUploadedHandler: row for submission {SubmissionId} already inserted by a concurrent delivery.", @event.SubmissionId);
        }
    }
}
