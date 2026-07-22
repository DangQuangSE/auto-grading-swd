namespace AutoGrading.NotificationSvc.Api.Domain;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? IntegrationEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
