using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface IRubricService
{
    Task<List<Rubric>> ListAsync(Guid? subjectId, Guid? assignmentId, Guid? userId, bool isAdmin, CancellationToken cancellationToken);

    Task<Rubric?> GetByIdAsync(Guid id, bool includeCriteria, CancellationToken cancellationToken);

    /// <summary>Validates scope/authorization and creates-or-updates the rubric's metadata row; does not touch object storage
    /// or Hangfire (the endpoint uploads the file first and enqueues <c>RubricParsingJob</c> after this call succeeds).
    /// Throws <see cref="RubricForbiddenException"/> if <paramref name="userId"/>/<paramref name="isAdmin"/> aren't authorized
    /// for an existing rubric, or a <see cref="RubricScope.SchoolWide"/> upload is attempted by a non-admin.</summary>
    Task<RubricUploadResult> UploadAsync(RubricUploadRequest request, Guid userId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogNotFoundException"/> if not found, <see cref="RubricForbiddenException"/> if not
    /// authorized, or <see cref="CatalogConflictException"/> if the rubric's status isn't <see cref="RubricStatus.Parsing"/>.
    /// Does not enqueue Hangfire — the endpoint does that after this call succeeds.</summary>
    Task<Rubric> RetryParsingAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogNotFoundException"/>, <see cref="RubricForbiddenException"/>, or
    /// <see cref="CatalogConflictException"/> if the rubric's status isn't <see cref="RubricStatus.Draft"/>.</summary>
    Task<List<RubricCriterion>> UpdateCriteriaAsync(Guid id, List<RubricCriterionInput> criteria, Guid userId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogNotFoundException"/>, <see cref="RubricForbiddenException"/>, or
    /// <see cref="CatalogConflictException"/> if <see cref="Rubric.Confirm"/>'s status guard fails.</summary>
    Task<Rubric> ConfirmAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="CatalogNotFoundException"/>, <see cref="RubricForbiddenException"/>, or
    /// <see cref="CatalogConflictException"/> if <see cref="Rubric.Unlock"/>'s status guard fails.</summary>
    Task<Rubric> UnlockAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="RubricForbiddenException"/> if the caller may not view the rubric. Returns <c>null</c> if
    /// not found or it has no uploaded file yet.</summary>
    Task<Rubric?> DownloadFileAsync(Guid id, Guid userId, bool isAdmin, CancellationToken cancellationToken);
}
