using AutoGrading.Catalog.Api.Constant;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Service;

public sealed class SubjectService(ISubjectRepository repo) : ISubjectService
{
    public Task<PagedResult<Subject>> ListAsync(string? search, int? page, int? pageSize, CancellationToken cancellationToken) =>
        repo.ListAsync(search, page, pageSize, cancellationToken);

    public Task<PagedResult<Subject>> ListOpenAsync(int? page, int? pageSize, CancellationToken cancellationToken) =>
        repo.ListOpenAsync(page, pageSize, cancellationToken);

    public Task<Subject?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        repo.GetByIdAsync(id, cancellationToken);

    public Task<bool> AnyAsync(Guid id, CancellationToken cancellationToken) =>
        repo.AnyAsync(id, cancellationToken);

    public Task<Subject> CreateAsync(string? code, string? name, CancellationToken cancellationToken)
    {
        var normalizedCode = code?.Trim().ToUpperInvariant();
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode) || string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new CatalogValidationException("invalid_subject", CatalogConstants.InvalidSubjectInput);
        }

        if (normalizedCode.Length > 32 || normalizedName.Length > 256)
        {
            throw new CatalogValidationException("subject_length_exceeded", CatalogConstants.SubjectLengthExceeded);
        }

        var subject = new Subject
        {
            Code = normalizedCode,
            Name = normalizedName,
            RegistrationStatus = RegistrationStatus.Closed
        };

        return repo.CreateAsync(subject, cancellationToken);
    }

    public Task<Subject?> UpdateRegistrationAsync(Guid id, RegistrationStatus status, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            throw new CatalogValidationException("invalid_registration_status", CatalogConstants.InvalidRegistrationStatus);
        }

        return repo.UpdateRegistrationAsync(id, status, cancellationToken);
    }
}
