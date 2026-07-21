using System.Security.Claims;
using AutoGrading.Common.Auth;
using AutoGrading.Common.Extensions;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.NotificationSvc.Api.Consumers;
using AutoGrading.NotificationSvc.Api.Data;
using AutoGrading.NotificationSvc.Api.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationDb")));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);
builder.Services.AddSignalR();

builder.Services.AddScoped<UserRegisteredConsumer>();
builder.Services.AddScoped<AiGradingCompletedConsumer>();
builder.Services.AddScoped<GradePublishedConsumer>();
builder.Services.AddScoped<RubricParsedConsumer>();
builder.Services.AddScoped<SubmissionStatusChangedConsumer>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MigrateDatabase<NotificationDbContext>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/notifications", async (Guid userId, NotificationDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct)))
    .RequireAuthorization();

app.MapGet("/notifications/unread-count", async (ClaimsPrincipal user, NotificationDbContext db, CancellationToken ct) =>
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return Results.Ok(new { unreadCount = count });
    })
    .RequireAuthorization();

app.MapDelete("/notifications/{id:guid}", async (Guid id, ClaimsPrincipal user, NotificationDbContext db, CancellationToken ct) =>
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification is null)
        {
            return Results.NotFound();
        }

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    })
    .RequireAuthorization();

app.MapGet("/audit-events", async (NotificationDbContext db, CancellationToken ct) =>
        Results.Ok(await db.AuditEvents.AsNoTracking()
            .OrderByDescending(a => a.OccurredAt)
            .Take(100)
            .ToListAsync(ct)))
    .RequireAuthorization(policy => policy.RequireRole("admin"));

app.MapHealthChecks("/health");
app.MapHub<NotificationHub>("/hub");

var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<UserRegistered, UserRegisteredConsumer>();
eventBus.Subscribe<AiGradingCompleted, AiGradingCompletedConsumer>();
eventBus.Subscribe<GradePublished, GradePublishedConsumer>();
eventBus.Subscribe<RubricParsed, RubricParsedConsumer>();
eventBus.Subscribe<SubmissionStatusChanged, SubmissionStatusChangedConsumer>();

app.Run();
