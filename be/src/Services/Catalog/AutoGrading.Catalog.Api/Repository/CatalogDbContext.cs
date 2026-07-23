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
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("subjects");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Code).IsRequired().HasMaxLength(32);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(256);
            entity.Property(s => s.RegistrationStatus)
                .IsRequired()
                .HasConversion(
                    status => status.ToString().ToLowerInvariant(),
                    value => Enum.Parse<RegistrationStatus>(value, true))
                .HasMaxLength(16)
                .HasDefaultValueSql("'closed'");
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
            entity.Property(c => c.NormalizedName).IsRequired().HasMaxLength(256);
            entity.HasIndex(c => c.LecturerId);
            entity.HasIndex(c => c.SubjectId);
            entity.HasIndex(c => new { c.SubjectId, c.NormalizedName })
                .IsUnique()
                .HasFilter("[SubjectId] IS NOT NULL");
            entity.HasAlternateKey(c => new { c.Id, c.EnrollmentSubjectId });
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_classes_EnrollmentSubject",
                "([SubjectId] IS NULL AND [EnrollmentSubjectId] = [Id]) OR [EnrollmentSubjectId] = [SubjectId]"));
            entity.HasOne(c => c.Subject)
                .WithMany(s => s.Classes)
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentEnrollment>(entity =>
        {
            entity.ToTable("student_enrollments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.StudentId, e.SubjectId }).IsUnique();
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.ClassId);
            entity.HasOne(e => e.Subject)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Class)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => new { e.ClassId, e.SubjectId })
                .HasPrincipalKey(c => new { c.Id, c.EnrollmentSubjectId })
                .OnDelete(DeleteBehavior.Restrict);
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
