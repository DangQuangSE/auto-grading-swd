using AutoGrading.Common.Messaging;
using AutoGrading.Common.OpenCode;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Domain;
using AutoGrading.Grading.Api.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoGrading.Grading.Api.Tests;

public class AiGradingJobTests
{
    private static GradingDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<GradingDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    // No API key configured -> OpenCodeClient falls back to its deterministic stub, so these
    // tests exercise AiGradingJob's rubric-lookup/failure logic without making real HTTP calls.
    private static IOpenCodeClient CreateStubOpenCodeClient() =>
        new OpenCodeClient(new HttpClient(), Options.Create(new OpenCodeOptions()), NullLogger<OpenCodeClient>.Instance);

    private sealed class RecordingEventBus : IEventBus
    {
        public List<IntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IntegrationEvent
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }

        public void Subscribe<TEvent, THandler>()
            where TEvent : IntegrationEvent
            where THandler : IIntegrationEventHandler<TEvent>
        {
        }
    }

    [Fact]
    public async Task ExecuteAsync_NoConfirmedCriteriaForAssignment_ThrowsAndMarksRunFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var submissionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        await using var db = CreateDbContext(dbName);
        var job = new AiGradingJob(db, CreateStubOpenCodeClient(), new OpenCodeOptions(), new RecordingEventBus());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => job.ExecuteAsync(submissionId, assignmentId, CancellationToken.None));

        var run = await db.AiGradingRuns.SingleAsync(r => r.SubmissionId == submissionId);
        Assert.Equal(AiGradingRunStatus.Failed, run.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ConfirmedCriteriaExist_GradesAgainstThoseCriteriaAndPublishesCompleted()
    {
        var dbName = Guid.NewGuid().ToString();
        var submissionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var criterionId = Guid.NewGuid();

        await using (var seedDb = CreateDbContext(dbName))
        {
            var localRubric = new LocalRubric { RubricId = Guid.NewGuid(), AssignmentId = assignmentId, Scope = "Lecturer" };
            seedDb.LocalRubrics.Add(localRubric);
            seedDb.LocalRubricCriteria.Add(new LocalRubricCriterion
            {
                LocalRubricId = localRubric.Id,
                RubricCriterionId = criterionId,
                Name = "Correctness",
                MaxScore = 40m,
                OrderIndex = 0,
            });
            await seedDb.SaveChangesAsync();
        }

        var eventBus = new RecordingEventBus();
        await using var db = CreateDbContext(dbName);
        var job = new AiGradingJob(db, CreateStubOpenCodeClient(), new OpenCodeOptions(), eventBus);

        await job.ExecuteAsync(submissionId, assignmentId, CancellationToken.None);

        var run = await db.AiGradingRuns.SingleAsync(r => r.SubmissionId == submissionId);
        Assert.Equal(AiGradingRunStatus.Completed, run.Status);

        var scores = await db.AiCriterionScores.Where(s => s.SubmissionId == submissionId).ToListAsync();
        var score = Assert.Single(scores);
        Assert.Equal(criterionId, score.RubricCriterionId);
        Assert.Equal(40m, score.MaxScore);

        var published = Assert.Single(eventBus.Published);
        var completed = Assert.IsType<AiGradingCompleted>(published);
        Assert.Equal(submissionId, completed.SubmissionId);
    }
}
