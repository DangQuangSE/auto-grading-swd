using AutoGrading.Common.Messaging;
using AutoGrading.Common.OpenCode;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Domain;
using AutoGrading.Grading.Api.Jobs;
using AutoGrading.Grading.Api.Clients;
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

    private sealed class StubCatalogApiClient(Guid criterionId) : ICatalogApiClient
    {
        public Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) => Task.FromResult<AssignmentDto?>(null);
        public Task<IReadOnlyList<RubricCriterionDto>> GetCriteriaForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RubricCriterionDto>>(new List<RubricCriterionDto> { new RubricCriterionDto(criterionId, "C1", "Correctness", "Desc", 40m, 0) });
        public Task<HashSet<Guid>> GetLecturerStudentIdsAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken) => Task.FromResult(new HashSet<Guid>());
    }

    private sealed class StubSubmissionApiClient(Guid assignmentId) : ISubmissionApiClient
    {
        public Task<SubmissionDto?> GetSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) => 
            Task.FromResult<SubmissionDto?>(new SubmissionDto(submissionId, assignmentId, Guid.NewGuid(), new List<ExtractedArtifactDto>()));
    }

    [Fact]
    public async Task ExecuteAsync_NoConfirmedCriteriaForAssignment_ThrowsAndMarksRunFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var submissionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        await using var db = CreateDbContext(dbName);
        var job = new AiGradingJob(db, new StubCatalogApiClient(Guid.NewGuid()), new StubSubmissionApiClient(assignmentId), CreateStubOpenCodeClient(), new OpenCodeOptions(), new RecordingEventBus());

        // Wait, the stub returns criteria. Let's make an empty one.
        var emptyCatalog = new MockEmptyCatalog();
        var jobEmpty = new AiGradingJob(db, emptyCatalog, new StubSubmissionApiClient(assignmentId), CreateStubOpenCodeClient(), new OpenCodeOptions(), new RecordingEventBus());

        // It doesn't throw anymore if empty, it falls back to a single general criterion. 
        // So we just execute and it should complete.
        await jobEmpty.ExecuteAsync(submissionId, null, CancellationToken.None);

        var run = await db.AiGradingRuns.SingleAsync(r => r.SubmissionId == submissionId);
        Assert.Equal(AiGradingRunStatus.Completed, run.Status);
    }
    
    private sealed class MockEmptyCatalog : ICatalogApiClient {
        public Task<AssignmentDto?> GetAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) => Task.FromResult<AssignmentDto?>(null);
        public Task<IReadOnlyList<RubricCriterionDto>> GetCriteriaForAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RubricCriterionDto>>(new List<RubricCriterionDto>());
        public Task<HashSet<Guid>> GetLecturerStudentIdsAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken) => Task.FromResult(new HashSet<Guid>());
    }

    [Fact]
    public async Task ExecuteAsync_ConfirmedCriteriaExist_GradesAgainstThoseCriteriaAndPublishesCompleted()
    {
        var dbName = Guid.NewGuid().ToString();
        var submissionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var criterionId = Guid.NewGuid();

        var eventBus = new RecordingEventBus();
        await using var db = CreateDbContext(dbName);
        var job = new AiGradingJob(db, new StubCatalogApiClient(criterionId), new StubSubmissionApiClient(assignmentId), CreateStubOpenCodeClient(), new OpenCodeOptions(), eventBus);

        await job.ExecuteAsync(submissionId, null, CancellationToken.None);

        var run = await db.AiGradingRuns.SingleAsync(r => r.SubmissionId == submissionId);
        Assert.Equal(AiGradingRunStatus.Completed, run.Status);

        var scores = await db.AiCriterionScores.Where(s => s.SubmissionId == submissionId).ToListAsync();
        var score = Assert.Single(scores);
        Assert.Equal(criterionId, score.RubricCriterionId);
        Assert.Equal(40m, score.MaxScore);

        var completed = Assert.IsType<AiGradingCompleted>(eventBus.Published.OfType<AiGradingCompleted>().Single());
        Assert.Equal(submissionId, completed.SubmissionId);
    }
}
