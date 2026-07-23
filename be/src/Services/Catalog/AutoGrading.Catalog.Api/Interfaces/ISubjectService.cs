using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface ISubjectService
{
    Task<PagedResult<Subject>> ListAsync(string? search, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<PagedResult<Subject>> ListOpenAsync(int? page, int? pageSize, CancellationToken cancellationToken);

    Task<Subject?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogValidationException"/> if <paramref name="code"/>/<paramref name="name"/> are
    /// missing or too long, or <see cref="CatalogConflictException"/> if the code is already taken.</summary>
    Task<Subject> CreateAsync(string? code, string? name, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogValidationException"/> if <paramref name="status"/> is not a defined
    /// <see cref="RegistrationStatus"/> value. Returns <c>null</c> if the subject does not exist.</summary>
    Task<Subject?> UpdateRegistrationAsync(Guid id, RegistrationStatus status, CancellationToken cancellationToken);
}
