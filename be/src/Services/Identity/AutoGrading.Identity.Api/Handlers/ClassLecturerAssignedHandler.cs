using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Data;
using AutoGrading.Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Handlers;

/// <summary>Consumes ClassLecturerAssigned and upserts Identity's local class-name/lecturer cache, keyed
/// by ClassId so redelivery of the same event is idempotent (same values written again).</summary>
public sealed class ClassLecturerAssignedHandler(IdentityDbContext db, ILogger<ClassLecturerAssignedHandler> logger)
    : IIntegrationEventHandler<ClassLecturerAssigned>
{
    public async Task HandleAsync(ClassLecturerAssigned @event, CancellationToken cancellationToken = default)
    {
        var cache = await db.ClassLecturerCaches.FirstOrDefaultAsync(c => c.ClassId == @event.ClassId, cancellationToken);
        if (cache is null)
        {
            cache = new ClassLecturerCache { ClassId = @event.ClassId };
            db.ClassLecturerCaches.Add(cache);
        }

        cache.ClassName = @event.ClassName;
        cache.LecturerId = @event.LecturerId;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsPrimaryKeyViolation())
        {
            logger.LogDebug("ClassLecturerAssignedHandler: row for class {ClassId} already inserted by a concurrent delivery.", @event.ClassId);
        }
    }
}
