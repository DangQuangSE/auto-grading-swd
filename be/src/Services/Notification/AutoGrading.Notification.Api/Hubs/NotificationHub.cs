using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AutoGrading.NotificationSvc.Api.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    // The base Hub implementation is sufficient for now.
    // SignalR automatically maps the authenticated user's NameIdentifier to the UserId connection group.
}
