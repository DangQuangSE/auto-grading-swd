using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Interfaces;

namespace AutoGrading.Grading.Api.Jobs;

public sealed class GradePublishedOutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<GradePublishedOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IGradingRepository>();
                var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                var messages = await repository.GetPendingOutboxMessagesAsync(100, stoppingToken);
                foreach (var message in messages)
                {
                    await bus.PublishAsync(new GradePublished(message.SubmissionId, message.FinalGradeId, message.FinalScore, message.PublishedByUserId) { EventId = message.Id }, stoppingToken);
                    await repository.MarkOutboxDispatchedAsync(message.Id, stoppingToken);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to dispatch grade publication outbox.");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
