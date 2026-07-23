using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface IAssignmentService
{
    Task<PagedResult<Assignment>> ListAsync(Guid? subjectId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogValidationException"/> if <paramref name="maxAttempts"/> &lt; 1.</summary>
    Task<Assignment> CreateAsync(Guid subjectId, string title, string? description, DateTimeOffset? dueDate, int maxAttempts, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogValidationException"/> if <paramref name="maxAttempts"/> &lt; 1. Returns <c>null</c> if not found.</summary>
    Task<Assignment?> UpdateAsync(Guid id, string title, string? description, DateTimeOffset? dueDate, int maxAttempts, CancellationToken cancellationToken);
}
