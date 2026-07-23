using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Handlers;

/// <summary>Consumes ClassLecturerAssigned and upserts Identity's local class-name/lecturer cache, keyed
/// by ClassId so redelivery of the same event is idempotent (same values written again).</summary>
public sealed class ClassLecturerAssignedHandler(IUserRepository repository, ILogger<ClassLecturerAssignedHandler> logger)
    : IIntegrationEventHandler<ClassLecturerAssigned>
{
    public async Task HandleAsync(ClassLecturerAssigned @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.UpsertClassLecturerCacheAsync(@event.ClassId, @event.ClassName, @event.LecturerId, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsPrimaryKeyViolation())
        {
            logger.LogDebug("ClassLecturerAssignedHandler: row for class {ClassId} already inserted by a concurrent delivery.", @event.ClassId);
        }
    }
}
