using System.Text.Json;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.NotificationSvc.Api.Data;
using AutoGrading.NotificationSvc.Api.Domain;

namespace AutoGrading.NotificationSvc.Api.Consumers;

/// <summary>
/// Records an audit trail entry when a rubric finishes parsing into criteria. No user-facing
/// notification is created here because the event does not carry a recipient user id.
/// </summary>
public sealed class RubricParsedConsumer(NotificationDbContext db, ILogger<RubricParsedConsumer> logger)
    : IIntegrationEventHandler<RubricParsed>
{
    public async Task HandleAsync(RubricParsed @event, CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = nameof(RubricParsed),
            Payload = JsonSerializer.Serialize(@event),
        });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Rubric {RubricId} parsed into {CriteriaCount} criteria for subject {SubjectId}",
            @event.RubricId, @event.CriteriaCount, @event.SubjectId);
    }
}
