using AutoGrading.Common.Auth;
using AutoGrading.Common.Extensions;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.NotificationSvc.Api.Consumers;
using AutoGrading.NotificationSvc.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb")));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);

builder.Services.AddScoped<UserRegisteredConsumer>();
builder.Services.AddScoped<AiGradingCompletedConsumer>();
builder.Services.AddScoped<GradePublishedConsumer>();

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

app.MapGet("/audit-events", async (NotificationDbContext db, CancellationToken ct) =>
        Results.Ok(await db.AuditEvents.AsNoTracking()
            .OrderByDescending(a => a.OccurredAt)
            .Take(100)
            .ToListAsync(ct)))
    .RequireAuthorization(policy => policy.RequireRole("admin"));

app.MapHealthChecks("/health");

var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<UserRegistered, UserRegisteredConsumer>();
eventBus.Subscribe<AiGradingCompleted, AiGradingCompletedConsumer>();
eventBus.Subscribe<GradePublished, GradePublishedConsumer>();

app.Run();
