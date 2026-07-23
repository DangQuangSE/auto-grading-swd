using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface IClassService
{
    Task<List<Class>> ListLegacyAsync(CancellationToken cancellationToken);

    Task<PagedResult<Class>> ListAdminAsync(Guid? subjectId, int? page, int? pageSize, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogNotFoundException"/> if the subject doesn't exist, or (when <paramref name="isStudent"/>
    /// is <c>true</c>) if the subject's registration is not <see cref="RegistrationStatus.Open"/> — both map to the original
    /// endpoint's bare <c>Results.NotFound()</c>.</summary>
    Task<PagedResult<Class>> ListForSubjectAsync(Guid subjectId, int? page, int? pageSize, bool isStudent, CancellationToken cancellationToken);

    Task<Class?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogValidationException"/> for invalid name/lecturer input, <see cref="CatalogConflictException"/>
    /// on save conflict, or <see cref="ClassEventPublishException"/> if the event publish fails.</summary>
    Task<Class> CreateLegacyAsync(string? name, Guid lecturerId, CancellationToken cancellationToken);

    /// <summary>Same as <see cref="CreateLegacyAsync"/>, plus <see cref="CatalogValidationException"/> (code <c>invalid_subject</c>) if the subject doesn't exist.</summary>
    Task<Class> CreateSubjectScopedAsync(string? name, Guid lecturerId, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogNotFoundException"/> if the class doesn't exist, <see cref="CatalogValidationException"/>
    /// for an empty update / invalid lecturer / invalid subject, <see cref="CatalogConflictException"/> if the class already
    /// has enrollments and the subject is being changed (code <c>class_subject_locked</c>) or on save conflict, or
    /// <see cref="ClassEventPublishException"/> if the event publish fails.</summary>
    Task<Class> UpdateAsync(Guid id, Guid? lecturerId, Guid? subjectId, CancellationToken cancellationToken);
}
