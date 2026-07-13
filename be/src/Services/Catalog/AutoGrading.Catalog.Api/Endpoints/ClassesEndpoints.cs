using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class ClassesEndpoints
{
    public static IEndpointRouteBuilder MapClassesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/classes").WithTags("Classes");

        group.MapGet("/", async (CatalogDbContext db, CancellationToken ct) =>
            {
                var classes = await db.Classes.AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new ClassSummary(c.Id, c.Name))
                    .ToListAsync(ct);

                return Results.Ok(classes);
            })
            .AllowAnonymous();

        group.MapPost("/", CreateClassAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));

        group.MapPatch("/{id:guid}", ReassignLecturerAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));

        return app;
    }

    private static async Task<IResult> CreateClassAsync(
        CreateClassRequest request,
        CatalogDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateClassInput(request.Name, request.LecturerId);
        if (validationError is not null)
        {
            return validationError;
        }

        var @class = new Class { Name = request.Name, LecturerId = request.LecturerId };
        db.Classes.Add(@class);

        var publishError = await SaveAndPublishAsync(
            db,
            eventBus,
            () => new ClassLecturerAssigned(@class.Id, @class.Name, @class.LecturerId),
            cancellationToken);
        if (publishError is not null)
        {
            return publishError;
        }

        return Results.Created($"/classes/{@class.Id}", @class);
    }

    private static async Task<IResult> ReassignLecturerAsync(
        Guid id,
        ReassignLecturerRequest request,
        CatalogDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateClassInput(name: null, request.LecturerId);
        if (validationError is not null)
        {
            return validationError;
        }

        var @class = await db.Classes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (@class is null)
        {
            return Results.NotFound();
        }

        @class.LecturerId = request.LecturerId;

        var publishError = await SaveAndPublishAsync(
            db,
            eventBus,
            () => new ClassLecturerAssigned(@class.Id, @class.Name, @class.LecturerId),
            cancellationToken);
        if (publishError is not null)
        {
            return publishError;
        }

        return Results.Ok(@class);
    }

    /// <summary>Persists the pending Class change and publishes its event inside one DB transaction —
    /// if publishing to the event bus throws, the transaction is rolled back so the Class row never persists
    /// without Identity being notified (per Phase 1's Event Publishing Failure mitigation).</summary>
    private static async Task<IResult?> SaveAndPublishAsync(
        CatalogDbContext db,
        IEventBus eventBus,
        Func<ClassLecturerAssigned> buildEvent,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await eventBus.PublishAsync(buildEvent(), cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(
                "Failed to publish ClassLecturerAssigned event; the class change was not saved. Please retry.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    private static IResult? ValidateClassInput(string? name, Guid lecturerId)
    {
        if (name is not null && string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest("Name is required.");
        }

        if (lecturerId == Guid.Empty)
        {
            return Results.BadRequest("LecturerId is required.");
        }

        return null;
    }
}

public sealed record ClassSummary(Guid Id, string Name);

public sealed record CreateClassRequest(string Name, Guid LecturerId);

public sealed record ReassignLecturerRequest(Guid LecturerId);
