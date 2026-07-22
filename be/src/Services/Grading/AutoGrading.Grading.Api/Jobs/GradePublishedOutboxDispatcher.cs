using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Data;
using Microsoft.EntityFrameworkCore;

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
                var db = scope.ServiceProvider.GetRequiredService<GradingDbContext>();
                var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                var messages = await db.GradePublishedOutbox.Where(x => x.DispatchedAt == null)
                    .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    await bus.PublishAsync(new GradePublished(message.SubmissionId, message.FinalGradeId, message.FinalScore, message.PublishedByUserId) { EventId = message.Id }, stoppingToken);
                    message.DispatchedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
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
