using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface IAssignmentRepository
{
    Task<PagedResult<Assignment>> ListAsync(Guid? subjectId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Assignment> CreateAsync(Assignment assignment, CancellationToken cancellationToken);

    /// <summary>Fetches, mutates, and saves the assignment in one call (avoids a double-fetch when Service has already validated input). Returns <c>null</c> if not found.</summary>
    Task<Assignment?> UpdateAsync(Guid id, string title, string? description, DateTimeOffset? dueDate, int maxAttempts, CancellationToken cancellationToken);
}
