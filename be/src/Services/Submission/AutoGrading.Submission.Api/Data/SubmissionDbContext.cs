using AutoGrading.SubmissionSvc.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.SubmissionSvc.Api.Data;

public class SubmissionDbContext(DbContextOptions<SubmissionDbContext> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<ExtractedArtifact> ExtractedArtifacts => Set<ExtractedArtifact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Submission>(entity =>
        {
            entity.ToTable("submissions");
            entity.Property(s => s.State).HasConversion<string>();
            entity.HasMany(s => s.Artifacts)
                .WithOne(a => a.Submission)
                .HasForeignKey(a => a.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExtractedArtifact>(entity =>
        {
            entity.ToTable("extracted_artifacts");
            entity.Property(a => a.Kind).HasConversion<string>();
        });
    }
}
