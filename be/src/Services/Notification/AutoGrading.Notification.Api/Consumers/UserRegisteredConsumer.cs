using System.Text.Json;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.NotificationSvc.Api.Data;
using AutoGrading.NotificationSvc.Api.Domain;

namespace AutoGrading.NotificationSvc.Api.Consumers;

/// <summary>Records an audit trail entry and a welcome notification when a new user registers.</summary>
public sealed class UserRegisteredConsumer(NotificationDbContext db, ILogger<UserRegisteredConsumer> logger)
    : IIntegrationEventHandler<UserRegistered>
{
    public async Task HandleAsync(UserRegistered @event, CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = nameof(UserRegistered),
            Payload = JsonSerializer.Serialize(@event),
        });

        db.Notifications.Add(new Notification
        {
            UserId = @event.UserId,
            Type = nameof(UserRegistered),
            Title = "Welcome to AutoGrading",
            Message = $"Hi {@event.FullName}, your account has been created.",
        });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stub email sent to {Email} welcoming {FullName}", @event.Email, @event.FullName);
    }
}
