using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using Hangfire;

namespace AutoGrading.Grading.Api.Jobs;

/// <summary>Enqueues the Hangfire <see cref="AiGradingJob"/> whenever a submission's artifacts finish extracting successfully.</summary>
public sealed class ArtifactsExtractedHandler(IBackgroundJobClient backgroundJobs) : IIntegrationEventHandler<ArtifactsExtracted>
{
    public Task HandleAsync(ArtifactsExtracted @event, CancellationToken cancellationToken = default)
    {
        if (@event.Success)
        {
            backgroundJobs.Enqueue<AiGradingJob>(job => job.ExecuteAsync(@event.SubmissionId, CancellationToken.None));
        }

        return Task.CompletedTask;
    }
}
