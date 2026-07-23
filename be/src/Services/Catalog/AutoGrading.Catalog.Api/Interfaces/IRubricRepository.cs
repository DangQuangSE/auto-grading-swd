using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface IRubricRepository
{
    /// <summary>Unpaged, matching the original endpoint. When <paramref name="isAdmin"/> is <c>false</c>, only
    /// <see cref="RubricStatus.Confirmed"/> rubrics plus the caller's own (by <paramref name="userId"/>) are included.</summary>
    Task<List<Rubric>> ListAsync(Guid? subjectId, Guid? assignmentId, Guid? userId, bool isAdmin, CancellationToken cancellationToken);

    Task<Rubric?> GetByIdAsync(Guid id, bool includeCriteria, CancellationToken cancellationToken);

    /// <summary>Used by upload to find an existing rubric for the same assignment. <paramref name="includeCriteria"/>
    /// should be <c>false</c> for an authorization-only check (avoids loading the criteria collection needlessly).</summary>
    Task<Rubric?> GetByAssignmentIdAsync(Guid assignmentId, bool includeCriteria, CancellationToken cancellationToken);

    Task<Rubric> CreateAsync(Rubric rubric, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogConflictException"/> on <c>DbUpdateConcurrencyException</c>.</summary>
    Task<Rubric> UpdateAsync(Rubric rubric, CancellationToken cancellationToken);

    /// <summary>Replaces criteria via <c>CatalogDbContext.ReplaceRubricCriteria</c> (never by mutating the loaded navigation
    /// collection directly — that throws <c>DbUpdateConcurrencyException</c> against a RowVersion-tracked entity). Throws
    /// <see cref="CatalogConflictException"/> on a concurrent-modification conflict.</summary>
    Task<List<RubricCriterion>> UpdateCriteriaAsync(Rubric rubric, List<RubricCriterion> criteria, CancellationToken cancellationToken);

    /// <summary>Persists a rubric whose <see cref="Rubric.Confirm"/> has already been called. Throws <see cref="CatalogConflictException"/> on a concurrent-modification conflict.</summary>
    Task<Rubric> ConfirmAsync(Rubric rubric, CancellationToken cancellationToken);

    /// <summary>Persists a rubric whose <see cref="Rubric.Unlock"/> has already been called. Throws <see cref="CatalogConflictException"/> on a concurrent-modification conflict.</summary>
    Task<Rubric> UnlockAsync(Rubric rubric, CancellationToken cancellationToken);

    /// <summary>Read-only fetch (<c>AsNoTracking</c>) used to resolve a rubric's <see cref="Rubric.FileObjectKey"/> for download. Returns <c>null</c> if not found.</summary>
    Task<Rubric?> DownloadFileAsync(Guid id, CancellationToken cancellationToken);
}
