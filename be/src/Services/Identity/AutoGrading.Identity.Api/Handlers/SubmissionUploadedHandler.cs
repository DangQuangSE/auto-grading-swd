using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Handlers;

/// <summary>Consumes SubmissionUploaded and records which student a submission belongs to, keyed by
/// SubmissionId. A submission's StudentId never changes, so redelivery is a no-op.</summary>
public sealed class SubmissionUploadedHandler(IUserRepository repository, ILogger<SubmissionUploadedHandler> logger)
    : IIntegrationEventHandler<SubmissionUploaded>
{
    public async Task HandleAsync(SubmissionUploaded @event, CancellationToken cancellationToken = default)
    {
        if (await repository.SubmissionStudentExistsAsync(@event.SubmissionId, cancellationToken))
        {
            logger.LogDebug("SubmissionUploadedHandler: submission {SubmissionId} already recorded; redelivery.", @event.SubmissionId);
            return;
        }

        try
        {
            await repository.InsertSubmissionStudentAsync(@event.SubmissionId, @event.StudentId, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsPrimaryKeyViolation())
        {
            logger.LogDebug("SubmissionUploadedHandler: row for submission {SubmissionId} already inserted by a concurrent delivery.", @event.SubmissionId);
        }
    }
}
