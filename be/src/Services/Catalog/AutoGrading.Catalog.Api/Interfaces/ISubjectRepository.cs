using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface ISubjectRepository
{
    Task<PagedResult<Subject>> ListAsync(string? search, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<PagedResult<Subject>> ListOpenAsync(int? page, int? pageSize, CancellationToken cancellationToken);

    Task<Subject?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Added in Phase 2 (not part of the original Phase 1 signature list) — <c>ClassesEndpoints</c> needs a
    /// subject-existence check that doesn't require injecting <c>CatalogDbContext</c> directly.</summary>
    Task<bool> AnyAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogConflictException"/> (code <c>subject_code_exists</c>) if <see cref="Subject.Code"/> is already taken.</summary>
    Task<Subject> CreateAsync(Subject subject, CancellationToken cancellationToken);

    /// <summary>Wraps the status change in a Serializable transaction, matching the original endpoint. Returns <c>null</c> if the subject does not exist.</summary>
    Task<Subject?> UpdateRegistrationAsync(Guid id, RegistrationStatus status, CancellationToken cancellationToken);
}
