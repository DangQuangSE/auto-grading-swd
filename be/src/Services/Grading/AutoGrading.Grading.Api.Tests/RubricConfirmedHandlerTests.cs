using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoGrading.Grading.Api.Tests;

public class RubricConfirmedHandlerTests
{
    private static GradingDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<GradingDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static RubricConfirmed BuildEvent(Guid rubricId, params RubricConfirmedCriterion[] criteria) =>
        new(rubricId, Guid.NewGuid(), Guid.NewGuid(), "Lecturer", criteria);

    [Fact]
    public async Task HandleAsync_NewRubric_InsertsLocalRubricAndCriteria()
    {
        var databaseName = Guid.NewGuid().ToString();
        var rubricId = Guid.NewGuid();
        var @event = BuildEvent(
            rubricId,
            new RubricConfirmedCriterion(Guid.NewGuid(), "Correctness", "desc", 40m, 0),
            new RubricConfirmedCriterion(Guid.NewGuid(), "Code Quality", null, 30m, 1));

        await using (var db = CreateDbContext(databaseName))
        {
            await new RubricConfirmedHandler(db, NullLogger<RubricConfirmedHandler>.Instance)
                .HandleAsync(@event, CancellationToken.None);
        }

        await using var verifyDb = CreateDbContext(databaseName);
        var localRubric = await verifyDb.LocalRubrics.Include(r => r.Criteria).SingleAsync(r => r.RubricId == rubricId);
        Assert.Equal(2, localRubric.Criteria.Count);
        Assert.Contains(localRubric.Criteria, c => c.Name == "Correctness" && c.MaxScore == 40m);
    }

    [Fact]
    public async Task HandleAsync_SameEventDeliveredTwice_ResultsInExactlyOneRowNoDuplicates()
    {
        // Each delivery uses its own DbContext, matching RabbitMqEventBus creating a fresh
        // DI scope per message — reusing one DbContext across deliveries isn't representative.
        var databaseName = Guid.NewGuid().ToString();
        var rubricId = Guid.NewGuid();
        var @event = BuildEvent(rubricId, new RubricConfirmedCriterion(Guid.NewGuid(), "Correctness", "desc", 40m, 0));

        await using (var db = CreateDbContext(databaseName))
        {
            await new RubricConfirmedHandler(db, NullLogger<RubricConfirmedHandler>.Instance)
                .HandleAsync(@event, CancellationToken.None);
        }

        await using (var db = CreateDbContext(databaseName))
        {
            await new RubricConfirmedHandler(db, NullLogger<RubricConfirmedHandler>.Instance)
                .HandleAsync(@event, CancellationToken.None);
        }

        await using var verifyDb = CreateDbContext(databaseName);
        var rows = await verifyDb.LocalRubrics.Where(r => r.RubricId == rubricId).ToListAsync();
        var rubric = Assert.Single(rows);

        var criteria = await verifyDb.LocalRubricCriteria.Where(c => c.LocalRubricId == rubric.Id).ToListAsync();
        Assert.Single(criteria);
    }

    [Fact]
    public async Task HandleAsync_RubricReconfirmedWithDifferentCriteria_OverwritesLocalCopy()
    {
        var databaseName = Guid.NewGuid().ToString();
        var rubricId = Guid.NewGuid();

        await using (var db = CreateDbContext(databaseName))
        {
            await new RubricConfirmedHandler(db, NullLogger<RubricConfirmedHandler>.Instance).HandleAsync(
                BuildEvent(rubricId, new RubricConfirmedCriterion(Guid.NewGuid(), "Old Criterion", null, 10m, 0)),
                CancellationToken.None);
        }

        await using (var db = CreateDbContext(databaseName))
        {
            await new RubricConfirmedHandler(db, NullLogger<RubricConfirmedHandler>.Instance).HandleAsync(
                BuildEvent(rubricId, new RubricConfirmedCriterion(Guid.NewGuid(), "New Criterion", null, 50m, 0)),
                CancellationToken.None);
        }

        await using var verifyDb = CreateDbContext(databaseName);
        var localRubric = await verifyDb.LocalRubrics.Include(r => r.Criteria).SingleAsync(r => r.RubricId == rubricId);
        var criterion = Assert.Single(localRubric.Criteria);
        Assert.Equal("New Criterion", criterion.Name);
        Assert.Equal(50m, criterion.MaxScore);
    }
}
