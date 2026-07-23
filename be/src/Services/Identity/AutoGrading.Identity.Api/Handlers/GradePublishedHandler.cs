using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Handlers;

/// <summary>Consumes GradePublished and appends a (SubmissionId, LecturerId) grading-authority row —
/// insert-only, never overwritten, so a re-grade by a different lecturer keeps the prior grader's row too.
/// Does not look up SubmissionStudent here; the SubmissionGrader/SubmissionStudent join happens at
/// authorization-check time so arrival order between the two events never matters.</summary>
public sealed class GradePublishedHandler(IUserRepository repository, ILogger<GradePublishedHandler> logger)
    : IIntegrationEventHandler<GradePublished>
{
    public async Task HandleAsync(GradePublished @event, CancellationToken cancellationToken = default)
    {
        if (await repository.SubmissionGraderExistsAsync(@event.SubmissionId, @event.PublishedByUserId, cancellationToken))
        {
            logger.LogDebug(
                "GradePublishedHandler: grader {LecturerId} already recorded for submission {SubmissionId}; redelivery.",
                @event.PublishedByUserId,
                @event.SubmissionId);
            return;
        }

        try
        {
            await repository.InsertSubmissionGraderAsync(@event.SubmissionId, @event.PublishedByUserId, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsPrimaryKeyViolation())
        {
            logger.LogDebug(
                "GradePublishedHandler: row for submission {SubmissionId}, lecturer {LecturerId} already inserted by a concurrent delivery.",
                @event.SubmissionId,
                @event.PublishedByUserId);
        }
    }
}
