using AutoGrading.NotificationSvc.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.NotificationSvc.Api.Data;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasIndex(x => x.IntegrationEventId).IsUnique().HasFilter("[IntegrationEventId] IS NOT NULL");
        });
    }
}
