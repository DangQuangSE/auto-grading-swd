using AutoGrading.Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<Class> Classes => Set<Class>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("subjects");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Code).IsRequired().HasMaxLength(32);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(s => s.Code).IsUnique();
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("assignments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Title).IsRequired().HasMaxLength(256);
            entity.HasOne(a => a.Subject)
                .WithMany(s => s.Assignments)
                .HasForeignKey(a => a.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Rubric>(entity =>
        {
            entity.ToTable("rubrics");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
            entity.Property(r => r.Scope).IsRequired().HasConversion<string>().HasMaxLength(32);
            entity.Property(r => r.RowVersion).IsRowVersion();
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.Scope);
            entity.HasOne(r => r.Subject)
                .WithMany()
                .HasForeignKey(r => r.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Assignment)
                .WithMany(a => a.Rubrics)
                .HasForeignKey(r => r.AssignmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RubricCriterion>(entity =>
        {
            entity.ToTable("rubric_criteria");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
            entity.Property(c => c.MaxScore).HasColumnType("decimal(5,2)");
            entity.HasOne(c => c.Rubric)
                .WithMany(r => r.Criteria)
                .HasForeignKey(c => c.RubricId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("classes");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(c => c.LecturerId);
        });
    }

    /// <summary>Replaces a rubric's criteria wholesale via the <see cref="RubricCriteria"/> DbSet directly
    /// (not <c>rubric.Criteria.Clear()</c>/<c>.Add()</c>) — mutating a loaded navigation collection on an
    /// entity with a RowVersion concurrency token causes SaveChanges to throw DbUpdateConcurrencyException.</summary>
    public List<RubricCriterion> ReplaceRubricCriteria(Rubric rubric, List<RubricCriterion> newCriteria)
    {
        RubricCriteria.RemoveRange(rubric.Criteria);
        RubricCriteria.AddRange(newCriteria);

        return newCriteria;
    }
}
