using AutoGrading.Grading.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Grading.Api.Data;

public class GradingDbContext(DbContextOptions<GradingDbContext> options) : DbContext(options)
{
    public DbSet<AiGradingRun> AiGradingRuns => Set<AiGradingRun>();
    public DbSet<AiCriterionScore> AiCriterionScores => Set<AiCriterionScore>();
    public DbSet<FinalGrade> FinalGrades => Set<FinalGrade>();
    public DbSet<GradePublication> GradePublications => Set<GradePublication>();
    public DbSet<GradePublishedOutbox> GradePublishedOutbox => Set<GradePublishedOutbox>();
    public DbSet<LocalRubric> LocalRubrics => Set<LocalRubric>();
    public DbSet<LocalRubricCriterion> LocalRubricCriteria => Set<LocalRubricCriterion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiGradingRun>(entity =>
        {
            entity.ToTable("ai_grading_runs");
            entity.Property(r => r.Status).HasConversion<string>();
            entity.HasMany(r => r.Scores)
                .WithOne(s => s.GradingRun)
                .HasForeignKey(s => s.GradingRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiCriterionScore>(entity =>
        {
            entity.ToTable("ai_criterion_scores");
            entity.Property(s => s.MaxScore).HasPrecision(5, 2);
            entity.Property(s => s.SuggestedScore).HasPrecision(5, 2);
            entity.Property(s => s.Confidence).HasPrecision(5, 2);
        });

        modelBuilder.Entity<FinalGrade>(entity =>
        {
            entity.ToTable("final_grades");
            entity.Property(f => f.FinalScore).HasPrecision(5, 2);
        });

        modelBuilder.Entity<GradePublication>(entity =>
        {
            entity.ToTable("grade_publications");
            entity.HasIndex(p => p.SubmissionId).IsUnique();
            entity.HasIndex(p => p.FinalGradeId).IsUnique();
        });
        modelBuilder.Entity<GradePublishedOutbox>(entity =>
        {
            entity.ToTable("grade_published_outbox");
            entity.Property(x => x.FinalScore).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.DispatchedAt, x.CreatedAt });
        });

        modelBuilder.Entity<LocalRubric>(entity =>
        {
            entity.ToTable("local_rubrics");
            entity.HasIndex(r => r.RubricId).IsUnique();
            entity.HasMany(r => r.Criteria)
                .WithOne(c => c.LocalRubric)
                .HasForeignKey(c => c.LocalRubricId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocalRubricCriterion>(entity =>
        {
            entity.ToTable("local_rubric_criteria");
            entity.Property(c => c.MaxScore).HasPrecision(5, 2);
        });
    }
}
