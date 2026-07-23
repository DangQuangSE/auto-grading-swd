using AutoGrading.Catalog.Api.Constant;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Repository;

public sealed class RubricRepository(CatalogDbContext db) : IRubricRepository
{
    public async Task<List<Rubric>> ListAsync(Guid? subjectId, Guid? assignmentId, Guid? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = db.Rubrics.AsNoTracking().Include(r => r.Criteria).AsQueryable();
        if (subjectId is not null)
        {
            query = query.Where(r => r.SubjectId == subjectId);
        }

        if (assignmentId is not null)
        {
            query = query.Where(r => r.AssignmentId == assignmentId);
        }

        if (!isAdmin)
        {
            query = query.Where(r => r.Status == RubricStatus.Confirmed || r.LecturerId == userId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<Rubric?> GetByIdAsync(Guid id, bool includeCriteria, CancellationToken cancellationToken)
    {
        IQueryable<Rubric> query = includeCriteria ? db.Rubrics.Include(r => r.Criteria) : db.Rubrics;
        return query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<Rubric?> GetByAssignmentIdAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        db.Rubrics.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.AssignmentId == assignmentId, cancellationToken);

    public async Task<Rubric> CreateAsync(Rubric rubric, CancellationToken cancellationToken)
    {
        db.Rubrics.Add(rubric);
        await db.SaveChangesAsync(cancellationToken);
        return rubric;
    }

    /// <summary>Used only for the upload-triggered re-upload path. Matches the original endpoint exactly: that
    /// <c>SaveChangesAsync</c> call is NOT wrapped in a concurrency try/catch (unlike <see cref="ConfirmAsync"/>/
    /// <see cref="UnlockAsync"/>/<see cref="UpdateCriteriaAsync"/>, which map to the endpoint's <c>TrySaveChangesAsync</c>
    /// helper) — a concurrent re-upload conflict propagates as an unhandled exception in the original code too.</summary>
    public async Task<Rubric> UpdateAsync(Rubric rubric, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        return rubric;
    }

    public async Task<List<RubricCriterion>> UpdateCriteriaAsync(Rubric rubric, List<RubricCriterion> criteria, CancellationToken cancellationToken)
    {
        var newCriteria = db.ReplaceRubricCriteria(rubric, criteria);
        await TrySaveChangesAsync(rubric.Id, cancellationToken);
        return newCriteria;
    }

    public async Task<Rubric> ConfirmAsync(Rubric rubric, CancellationToken cancellationToken)
    {
        await TrySaveChangesAsync(rubric.Id, cancellationToken);
        return rubric;
    }

    public async Task<Rubric> UnlockAsync(Rubric rubric, CancellationToken cancellationToken)
    {
        await TrySaveChangesAsync(rubric.Id, cancellationToken);
        return rubric;
    }

    public Task<Rubric?> DownloadFileAsync(Guid id, CancellationToken cancellationToken) =>
        db.Rubrics.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <summary>Ports the endpoint's <c>TrySaveChangesAsync</c> helper: <c>DbUpdateConcurrencyException</c> maps to
    /// <see cref="CatalogConflictException"/> with the same message shape as the original <c>Results.Conflict(...)</c>.</summary>
    private async Task TrySaveChangesAsync(Guid rubricId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CatalogConflictException(
                "rubric_concurrency_conflict",
                string.Format(CatalogConstants.RubricConcurrentModification, rubricId));
        }
    }
}
