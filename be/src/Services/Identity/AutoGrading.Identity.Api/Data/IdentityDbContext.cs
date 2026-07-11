using AutoGrading.Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash);
            entity.Property(u => u.GoogleSubjectId).HasMaxLength(64);
            entity.HasIndex(u => u.GoogleSubjectId).IsUnique().HasFilter("[GoogleSubjectId] IS NOT NULL");
            entity.Property(u => u.FullName).HasMaxLength(256);
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(32);
        });
    }
}
