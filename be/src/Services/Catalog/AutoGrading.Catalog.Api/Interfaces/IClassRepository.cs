using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface IClassRepository
{
    /// <summary>The legacy, unpaged listing behind <c>GET /classes/</c> (anonymous-accessible).</summary>
    Task<List<Class>> ListAsync(CancellationToken cancellationToken);

    Task<PagedResult<Class>> ListAdminAsync(Guid? subjectId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<PagedResult<Class>> ListForSubjectAsync(Guid subjectId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<Class?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Persists the class inside a transaction and publishes <c>ClassLecturerAssigned</c> before committing (mirrors
    /// <c>SaveAndPublishAsync</c>). Throws <see cref="CatalogConflictException"/> on a constraint violation, or
    /// <see cref="ClassEventPublishException"/> if the event publish fails (both roll back first).</summary>
    Task<Class> CreateAsync(Class newClass, CancellationToken cancellationToken);

    /// <summary>Same transaction/publish/rollback semantics as <see cref="CreateAsync"/>, for an already-mutated tracked entity.</summary>
    Task<Class> UpdateAsync(Class updatedClass, CancellationToken cancellationToken);

    Task<bool> AnyAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> AnyWithEnrollmentsAsync(Guid classId, CancellationToken cancellationToken);
}
