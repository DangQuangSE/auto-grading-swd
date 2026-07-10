using System.Text.Json;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.NotificationSvc.Api.Data;
using AutoGrading.NotificationSvc.Api.Domain;

namespace AutoGrading.NotificationSvc.Api.Consumers;

/// <summary>Records an audit trail entry and notifies the publisher when a final grade is published.</summary>
public sealed class GradePublishedConsumer(NotificationDbContext db, ILogger<GradePublishedConsumer> logger)
    : IIntegrationEventHandler<GradePublished>
{
    public async Task HandleAsync(GradePublished @event, CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = nameof(GradePublished),
            Payload = JsonSerializer.Serialize(@event),
        });

        db.Notifications.Add(new Notification
        {
            UserId = @event.PublishedByUserId,
            Type = nameof(GradePublished),
            Title = "Grade published",
            Message = $"Final score {@event.FinalScore} was published for submission {@event.SubmissionId}.",
        });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Grade published for submission {SubmissionId}: {Score}", @event.SubmissionId, @event.FinalScore);
    }
}
