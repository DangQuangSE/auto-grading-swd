using AutoGrading.Contracts.Events;
using AutoGrading.NotificationSvc.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using AutoGrading.Common.Messaging;

namespace AutoGrading.NotificationSvc.Api.Consumers;

public sealed class SubmissionStatusChangedConsumer(IHubContext<NotificationHub> hubContext) : IIntegrationEventHandler<SubmissionStatusChanged>
{
    public async Task HandleAsync(SubmissionStatusChanged @event, CancellationToken cancellationToken = default)
    {
        // Push the status update directly to the specific student's active SignalR connections
        await hubContext.Clients.User(@event.StudentId.ToString())
            .SendAsync("SubmissionUpdated", @event, cancellationToken);
    }
}
