using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using Hangfire;

namespace AutoGrading.SubmissionSvc.Api.Jobs;

/// <summary>Enqueues the Hangfire <see cref="ExtractionJob"/> whenever a submission's files finish uploading.</summary>
public sealed class SubmissionUploadedHandler(IBackgroundJobClient backgroundJobs) : IIntegrationEventHandler<SubmissionUploaded>
{
    public Task HandleAsync(SubmissionUploaded @event, CancellationToken cancellationToken = default)
    {
        backgroundJobs.Enqueue<ExtractionJob>(job => job.ExecuteAsync(@event.SubmissionId, CancellationToken.None));
        return Task.CompletedTask;
    }
}
