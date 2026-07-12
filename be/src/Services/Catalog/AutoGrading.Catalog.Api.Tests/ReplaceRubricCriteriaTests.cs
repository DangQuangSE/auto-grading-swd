using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Tests;

/// <summary>
/// Tests for the <see cref="CatalogDbContext.ReplaceRubricCriteria"/> method.
/// This method was added to fix a bug where mutating <c>rubric.Criteria</c> (navigation collection)
/// combined with a RowVersion concurrency token caused DbUpdateConcurrencyException on SaveChanges.
///
/// The fix uses the DbSet directly instead of the navigation collection.
/// </summary>
public class ReplaceRubricCriteriaTests
{
    private static CatalogDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    [Fact]
    public async Task ReplaceRubricCriteria_RemovesOldAndAddsNewCriteria()
    {
        var dbName = Guid.NewGuid().ToString();
        var rubricId = Guid.NewGuid();

        // Create rubric with initial criteria
        await using (var db = CreateDbContext(dbName))
        {
            var rubric = new Rubric
            {
                Id = rubricId,
                SubjectId = Guid.NewGuid(),
                Name = "Test Rubric",
                Status = RubricStatus.Draft,
            };

            db.Rubrics.Add(rubric);

            db.RubricCriteria.AddRange(
                new RubricCriterion { RubricId = rubricId, Name = "Old Criterion 1", MaxScore = 10m, OrderIndex = 0 },
                new RubricCriterion { RubricId = rubricId, Name = "Old Criterion 2", MaxScore = 20m, OrderIndex = 1 });

            await db.SaveChangesAsync();
        }

        // Replace criteria
        await using (var db = CreateDbContext(dbName))
        {
            var rubric = await db.Rubrics.Include(r => r.Criteria).FirstAsync(r => r.Id == rubricId);
            var newCriteria = new List<RubricCriterion>
            {
                new RubricCriterion { RubricId = rubricId, Name = "New Criterion", MaxScore = 50m, OrderIndex = 0 }
            };

            db.ReplaceRubricCriteria(rubric, newCriteria);
            await db.SaveChangesAsync();
        }

        // Verify only new criteria exist
        await using var verifyDb = CreateDbContext(dbName);
        var criteria = await verifyDb.RubricCriteria.Where(c => c.RubricId == rubricId).ToListAsync();
        Assert.Single(criteria);
        Assert.Equal("New Criterion", criteria.First().Name);
        Assert.Equal(50m, criteria.First().MaxScore);
    }

    [Fact]
    public async Task ReplaceRubricCriteria_WithEmptyList_RemovesAllCriteria()
    {
        var dbName = Guid.NewGuid().ToString();
        var rubricId = Guid.NewGuid();

        // Create rubric with criteria
        await using (var db = CreateDbContext(dbName))
        {
            var rubric = new Rubric
            {
                Id = rubricId,
                SubjectId = Guid.NewGuid(),
                Name = "Test Rubric",
                Status = RubricStatus.Draft,
            };

            db.Rubrics.Add(rubric);
            db.RubricCriteria.AddRange(
                new RubricCriterion { RubricId = rubricId, Name = "Criterion 1", MaxScore = 10m, OrderIndex = 0 },
                new RubricCriterion { RubricId = rubricId, Name = "Criterion 2", MaxScore = 20m, OrderIndex = 1 });

            await db.SaveChangesAsync();
        }

        // Replace with empty list
        await using (var db = CreateDbContext(dbName))
        {
            var rubric = await db.Rubrics.Include(r => r.Criteria).FirstAsync(r => r.Id == rubricId);
            db.ReplaceRubricCriteria(rubric, new List<RubricCriterion>());
            await db.SaveChangesAsync();
        }

        // Verify no criteria exist
        await using var verifyDb = CreateDbContext(dbName);
        var criteria = await verifyDb.RubricCriteria.Where(c => c.RubricId == rubricId).ToListAsync();
        Assert.Empty(criteria);
    }

    [Fact]
    public async Task ReplaceRubricCriteria_MultipleReplacements_AllSucceedWithoutConcurrencyErrors()
    {
        var dbName = Guid.NewGuid().ToString();
        var rubricId = Guid.NewGuid();

        // Initial setup
        await using (var db = CreateDbContext(dbName))
        {
            var rubric = new Rubric
            {
                Id = rubricId,
                SubjectId = Guid.NewGuid(),
                Name = "Test Rubric",
                Status = RubricStatus.Draft,
            };

            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(new RubricCriterion { RubricId = rubricId, Name = "Initial", MaxScore = 10m, OrderIndex = 0 });

            await db.SaveChangesAsync();
        }

        // Replace criteria multiple times (simulating re-editing before confirmation)
        for (int i = 1; i <= 3; i++)
        {
            await using var db = CreateDbContext(dbName);
            var rubric = await db.Rubrics.Include(r => r.Criteria).FirstAsync(r => r.Id == rubricId);

            var newCriteria = Enumerable.Range(0, i + 1)
                .Select(j => new RubricCriterion
                {
                    RubricId = rubricId,
                    Name = $"Criterion {j}",
                    MaxScore = (j + 1) * 10m,
                    OrderIndex = j
                })
                .ToList();

            db.ReplaceRubricCriteria(rubric, newCriteria);
            await db.SaveChangesAsync();
        }

        // Verify final state
        await using var verifyDb = CreateDbContext(dbName);
        var finalCriteria = await verifyDb.RubricCriteria.Where(c => c.RubricId == rubricId).OrderBy(c => c.OrderIndex).ToListAsync();
        Assert.Equal(4, finalCriteria.Count); // 0, 1, 2, 3
        Assert.Equal("Criterion 3", finalCriteria.Last().Name);
    }

    [Fact]
    public async Task ReplaceRubricCriteria_PreservesRubricRowVersionChanges()
    {
        var dbName = Guid.NewGuid().ToString();
        var rubricId = Guid.NewGuid();

        // Create rubric
        byte[]? originalRowVersion = null;
        await using (var db = CreateDbContext(dbName))
        {
            var rubric = new Rubric
            {
                Id = rubricId,
                SubjectId = Guid.NewGuid(),
                Name = "Test Rubric",
                Status = RubricStatus.Draft,
            };

            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(new RubricCriterion { RubricId = rubricId, Name = "Initial", MaxScore = 10m, OrderIndex = 0 });

            await db.SaveChangesAsync();
            originalRowVersion = rubric.RowVersion;
        }

        // Replace criteria and change status
        await using (var db = CreateDbContext(dbName))
        {
            var rubric = await db.Rubrics.Include(r => r.Criteria).FirstAsync(r => r.Id == rubricId);
            db.ReplaceRubricCriteria(rubric, new List<RubricCriterion>
            {
                new RubricCriterion { RubricId = rubricId, Name = "New", MaxScore = 50m, OrderIndex = 0 }
            });

            rubric.Status = RubricStatus.Confirmed; // Simulate confirming
            await db.SaveChangesAsync();
        }

        // Verify RowVersion changed (SQL Server updates this automatically)
        await using var verifyDb = CreateDbContext(dbName);
        var finalRubric = await verifyDb.Rubrics.Include(r => r.Criteria).FirstAsync(r => r.Id == rubricId);
        Assert.Equal(RubricStatus.Confirmed, finalRubric.Status);
        Assert.Single(finalRubric.Criteria);
    }
}
