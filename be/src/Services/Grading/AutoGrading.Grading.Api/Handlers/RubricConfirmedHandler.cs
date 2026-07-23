using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Repository;
using AutoGrading.Grading.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Grading.Api.Handlers;

/// <summary>
/// Consumes RubricConfirmed and upserts the confirmed criteria into Grading's local copy, keyed
/// by RubricId so redelivery of the same event is idempotent (no duplicate rows).
/// </summary>
public sealed class RubricConfirmedHandler(GradingDbContext db, ILogger<RubricConfirmedHandler> logger)
    : IIntegrationEventHandler<RubricConfirmed>
{
    public async Task HandleAsync(RubricConfirmed @event, CancellationToken cancellationToken = default)
    {
        if (@event.Criteria.Count == 0)
        {
            logger.LogWarning("RubricConfirmedHandler: rubric {RubricId} confirmed with zero criteria; storing an empty local copy.", @event.RubricId);
        }

        var localRubric = await db.LocalRubrics
            .Include(r => r.Criteria)
            .FirstOrDefaultAsync(r => r.RubricId == @event.RubricId, cancellationToken);

        if (localRubric is null)
        {
            localRubric = new LocalRubric { RubricId = @event.RubricId };
            db.LocalRubrics.Add(localRubric);
        }

        localRubric.SubjectId = @event.SubjectId;
        localRubric.AssignmentId = @event.AssignmentId;
        localRubric.Scope = @event.Scope;
        localRubric.ConfirmedAt = @event.OccurredAt;

        if (localRubric.Criteria.Count > 0)
        {
            db.LocalRubricCriteria.RemoveRange(localRubric.Criteria);
        }

        var newCriteria = @event.Criteria.Select(criterion => new LocalRubricCriterion
        {
            LocalRubricId = localRubric.Id,
            RubricCriterionId = criterion.RubricCriterionId,
            Name = criterion.Name,
            Description = criterion.Description,
            MaxScore = criterion.MaxScore,
            OrderIndex = criterion.OrderIndex,
        });
        db.LocalRubricCriteria.AddRange(newCriteria);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "RubricConfirmedHandler: upserted {CriteriaCount} criteria for rubric {RubricId}.",
            @event.Criteria.Count,
            @event.RubricId);
    }
}
