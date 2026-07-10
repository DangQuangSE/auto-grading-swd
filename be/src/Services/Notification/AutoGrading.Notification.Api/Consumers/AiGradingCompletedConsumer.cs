using System.Text.Json;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.NotificationSvc.Api.Data;
using AutoGrading.NotificationSvc.Api.Domain;

namespace AutoGrading.NotificationSvc.Api.Consumers;

/// <summary>
/// Records an audit trail entry when AI grading completes. No user-facing notification is
/// created here because the event does not carry a recipient user id (submissions don't expose
/// lecturer ownership cross-service yet).
/// </summary>
public sealed class AiGradingCompletedConsumer(NotificationDbContext db, ILogger<AiGradingCompletedConsumer> logger)
    : IIntegrationEventHandler<AiGradingCompleted>
{
    public async Task HandleAsync(AiGradingCompleted @event, CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = nameof(AiGradingCompleted),
            Payload = JsonSerializer.Serialize(@event),
        });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "AI grading completed for submission {SubmissionId}, run {RunId}, score {Score}",
            @event.SubmissionId, @event.AiGradingRunId, @event.TotalSuggestedScore);
    }
}
