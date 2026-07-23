using AutoGrading.Catalog.Api.Constant;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Service;

public sealed class ClassService(IClassRepository classRepo, ISubjectService subjectService) : IClassService
{
    public Task<List<Class>> ListLegacyAsync(CancellationToken cancellationToken) =>
        classRepo.ListAsync(cancellationToken);

    public Task<PagedResult<Class>> ListAdminAsync(Guid? subjectId, int? page, int? pageSize, CancellationToken cancellationToken) =>
        classRepo.ListAdminAsync(subjectId, page, pageSize, cancellationToken);

    public async Task<PagedResult<Class>> ListForSubjectAsync(Guid subjectId, int? page, int? pageSize, bool isStudent, CancellationToken cancellationToken)
    {
        var subject = await subjectService.GetByIdAsync(subjectId, cancellationToken);
        if (subject is null || (isStudent && subject.RegistrationStatus != RegistrationStatus.Open))
        {
            throw new CatalogNotFoundException(null, null);
        }

        return await classRepo.ListForSubjectAsync(subjectId, page, pageSize, cancellationToken);
    }

    public Task<Class?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        classRepo.GetByIdAsync(id, cancellationToken);

    public Task<Class> CreateLegacyAsync(string? name, Guid lecturerId, CancellationToken cancellationToken)
    {
        var normalizedName = ValidateAndNormalize(name, lecturerId, subjectId: null);

        var @class = new Class
        {
            Name = name!.Trim(),
            NormalizedName = normalizedName,
            LecturerId = lecturerId,
        };

        return classRepo.CreateAsync(@class, cancellationToken);
    }

    public async Task<Class> CreateSubjectScopedAsync(string? name, Guid lecturerId, Guid subjectId, CancellationToken cancellationToken)
    {
        var normalizedName = ValidateAndNormalize(name, lecturerId, subjectId);

        if (!await subjectService.AnyAsync(subjectId, cancellationToken))
        {
            throw new CatalogValidationException("invalid_subject", CatalogConstants.InvalidSubjectForClass);
        }

        var @class = new Class
        {
            Name = name!.Trim(),
            NormalizedName = normalizedName,
            LecturerId = lecturerId,
            SubjectId = subjectId,
            EnrollmentSubjectId = subjectId,
        };

        return await classRepo.CreateAsync(@class, cancellationToken);
    }

    public async Task<Class> UpdateAsync(Guid id, Guid? lecturerId, Guid? subjectId, CancellationToken cancellationToken)
    {
        if (!lecturerId.HasValue && !subjectId.HasValue)
        {
            throw new CatalogValidationException("empty_update", CatalogConstants.EmptyClassUpdate);
        }

        var @class = await classRepo.GetByIdAsync(id, cancellationToken);
        if (@class is null)
        {
            throw new CatalogNotFoundException(null, null);
        }

        if (lecturerId.HasValue)
        {
            if (lecturerId == Guid.Empty)
            {
                throw new CatalogValidationException("invalid_lecturer", CatalogConstants.InvalidLecturer);
            }

            @class.LecturerId = lecturerId.Value;
        }

        if (subjectId.HasValue && subjectId != @class.SubjectId)
        {
            if (subjectId == Guid.Empty || !await subjectService.AnyAsync(subjectId.Value, cancellationToken))
            {
                throw new CatalogValidationException("invalid_subject", CatalogConstants.InvalidSubjectForClass);
            }

            if (await classRepo.AnyWithEnrollmentsAsync(id, cancellationToken))
            {
                throw new CatalogConflictException("class_subject_locked", CatalogConstants.ClassSubjectLocked);
            }

            @class.SubjectId = subjectId.Value;
            @class.EnrollmentSubjectId = subjectId.Value;
        }

        return await classRepo.UpdateAsync(@class, cancellationToken);
    }

    private static string ValidateAndNormalize(string? name, Guid lecturerId, Guid? subjectId)
    {
        var normalizedName = NormalizeName(name);
        if (normalizedName is null)
        {
            throw new CatalogValidationException("invalid_class_name", CatalogConstants.InvalidClassName);
        }

        if (normalizedName.Length > 256)
        {
            throw new CatalogValidationException("class_name_too_long", CatalogConstants.ClassNameTooLong);
        }

        if (lecturerId == Guid.Empty)
        {
            throw new CatalogValidationException("invalid_lecturer", CatalogConstants.InvalidLecturer);
        }

        if (subjectId == Guid.Empty)
        {
            throw new CatalogValidationException("invalid_subject", CatalogConstants.InvalidSubjectIdRequired);
        }

        return normalizedName;
    }

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToUpperInvariant();
}
