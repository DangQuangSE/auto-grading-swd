using System.Data;
using AutoGrading.Catalog.Api.Constant;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AutoGrading.Catalog.Api.Repository;

public sealed class EnrollmentRepository(CatalogDbContext db) : IEnrollmentRepository
{
    public async Task<PagedResult<EnrollmentSummary>> ListStudentAsync(Guid studentId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.StudentEnrollments.AsNoTracking().Where(item => item.StudentId == studentId);
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(normalizedPage - 1) * normalizedPageSize;
        if (offset >= totalCount)
        {
            return new PagedResult<EnrollmentSummary>(Array.Empty<EnrollmentSummary>(), normalizedPage, normalizedPageSize, totalCount);
        }

        var items = await query
            .OrderBy(item => item.Subject.Code)
            .Skip((int)offset)
            .Take(normalizedPageSize)
            .Select(item => new EnrollmentProjection(
                item.Id,
                item.SubjectId,
                item.Subject.Code,
                item.Subject.Name,
                item.Subject.RegistrationStatus,
                item.ClassId,
                item.Class.Name,
                item.RowVersion,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentSummary>(items.Select(ToSummary).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<PagedResult<AdminEnrollmentSummary>> ListAdminAsync(
        Guid? studentId,
        Guid? subjectId,
        Guid? classId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.StudentEnrollments.AsNoTracking().AsQueryable();
        if (studentId.HasValue) query = query.Where(item => item.StudentId == studentId.Value);
        if (subjectId.HasValue) query = query.Where(item => item.SubjectId == subjectId.Value);
        if (classId.HasValue) query = query.Where(item => item.ClassId == classId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(normalizedPage - 1) * normalizedPageSize;
        if (offset >= totalCount)
        {
            return new PagedResult<AdminEnrollmentSummary>(Array.Empty<AdminEnrollmentSummary>(), normalizedPage, normalizedPageSize, totalCount);
        }

        var items = await query
            .OrderBy(item => item.StudentId)
            .ThenBy(item => item.Subject.Code)
            .Skip((int)offset)
            .Take(normalizedPageSize)
            .Select(item => new AdminEnrollmentProjection(
                item.Id,
                item.StudentId,
                item.SubjectId,
                item.Subject.Code,
                item.Subject.Name,
                item.Subject.RegistrationStatus,
                item.ClassId,
                item.Class.Name,
                item.RowVersion,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminEnrollmentSummary>(items.Select(ToSummary).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public Task<List<Guid>> ListStudentIdsForLecturerAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken) =>
        db.StudentEnrollments.AsNoTracking()
            .Where(item => item.SubjectId == subjectId && item.Class.LecturerId == lecturerId)
            .Select(item => item.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>Byte-for-byte port of <c>EnrollmentCommands.UpsertStudentAsync</c> — see the interface's XML doc for the
    /// atomicity/rollback guarantees this method must preserve.</summary>
    public async Task<EnrollmentCommandResult<EnrollmentSummary>> UpsertStudentAsync(
        Guid studentId,
        Guid subjectId,
        Guid classId,
        string? rowVersion,
        CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty || classId == Guid.Empty)
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Invalid("invalid_enrollment", CatalogConstants.InvalidEnrollment);
        }

        if (!TryDecodeRowVersion(rowVersion, out var expectedVersion))
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Invalid("invalid_row_version", CatalogConstants.InvalidRowVersion);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var subject = await db.Subjects.FirstOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
        if (subject is null)
        {
            return EnrollmentCommandResult<EnrollmentSummary>.NotFound("subject_not_found", CatalogConstants.SubjectNotFound);
        }

        if (subject.RegistrationStatus != RegistrationStatus.Open)
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Conflict("registration_closed", CatalogConstants.RegistrationClosed);
        }

        if (!await ClassMatchesSubjectAsync(classId, subjectId, cancellationToken))
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Invalid("class_subject_mismatch", CatalogConstants.ClassSubjectMismatch);
        }

        var enrollment = await FindEnrollmentAsync(studentId, subjectId, cancellationToken);
        if (enrollment is not null && enrollment.ClassId == classId)
        {
            await transaction.CommitAsync(cancellationToken);
            return EnrollmentCommandResult<EnrollmentSummary>.Success(await GetStudentByIdAsync(enrollment.Id, cancellationToken));
        }

        if (enrollment is null)
        {
            if (expectedVersion is not null)
            {
                return EnrollmentCommandResult<EnrollmentSummary>.Conflict("enrollment_missing", CatalogConstants.EnrollmentMissing);
            }

            enrollment = new StudentEnrollment
            {
                StudentId = studentId,
                SubjectId = subjectId,
                ClassId = classId
            };
            db.StudentEnrollments.Add(enrollment);
        }
        else
        {
            if (expectedVersion is null)
            {
                return EnrollmentCommandResult<EnrollmentSummary>.Conflict(
                    "row_version_required",
                    CatalogConstants.RowVersionRequired,
                    await GetStudentAsync(studentId, subjectId, cancellationToken));
            }

            ApplyUpdate(enrollment, classId, expectedVersion);
        }

        var failure = await SaveStudentAsync(enrollment, studentId, subjectId, transaction, cancellationToken);
        return failure ?? EnrollmentCommandResult<EnrollmentSummary>.Success(await GetStudentByIdAsync(enrollment.Id, cancellationToken));
    }

    /// <summary>Byte-for-byte port of <c>EnrollmentCommands.CorrectAdminAsync</c> — same atomicity/rollback guarantees as <see cref="UpsertStudentAsync"/>.</summary>
    public async Task<EnrollmentCommandResult<AdminEnrollmentSummary>> CorrectAdminAsync(
        Guid studentId,
        Guid subjectId,
        Guid classId,
        string? rowVersion,
        CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty || subjectId == Guid.Empty || classId == Guid.Empty)
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Invalid("invalid_enrollment", CatalogConstants.InvalidEnrollmentAdmin);
        }

        if (!TryDecodeRowVersion(rowVersion, out var expectedVersion))
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Invalid("invalid_row_version", CatalogConstants.InvalidRowVersion);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (!await db.Subjects.AnyAsync(item => item.Id == subjectId, cancellationToken))
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.NotFound("subject_not_found", CatalogConstants.SubjectNotFound);
        }

        if (!await ClassMatchesSubjectAsync(classId, subjectId, cancellationToken))
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Invalid("class_subject_mismatch", CatalogConstants.ClassSubjectMismatch);
        }

        var enrollment = await FindEnrollmentAsync(studentId, subjectId, cancellationToken);
        if (enrollment is null)
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.NotFound("enrollment_not_found", CatalogConstants.EnrollmentNotFound);
        }

        if (enrollment.ClassId == classId)
        {
            await transaction.CommitAsync(cancellationToken);
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Success(await GetAdminAsync(studentId, subjectId, cancellationToken));
        }

        if (expectedVersion is null)
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Conflict(
                "row_version_required",
                CatalogConstants.RowVersionRequired,
                await GetAdminAsync(studentId, subjectId, cancellationToken));
        }

        ApplyUpdate(enrollment, classId, expectedVersion);
        var failure = await SaveAdminAsync(studentId, subjectId, transaction, cancellationToken);
        return failure ?? EnrollmentCommandResult<AdminEnrollmentSummary>.Success(await GetAdminAsync(studentId, subjectId, cancellationToken));
    }

    /// <summary>Queries <c>Class</c> directly via this repository's own <see cref="CatalogDbContext"/> reference — never via
    /// <see cref="IClassRepository"/> — so the check stays inside the same Serializable transaction as the write that follows it.</summary>
    private Task<bool> ClassMatchesSubjectAsync(Guid classId, Guid subjectId, CancellationToken cancellationToken) =>
        db.Classes.AnyAsync(item => item.Id == classId && item.SubjectId == subjectId, cancellationToken);

    private Task<StudentEnrollment?> FindEnrollmentAsync(Guid studentId, Guid subjectId, CancellationToken cancellationToken) =>
        db.StudentEnrollments.FirstOrDefaultAsync(
            item => item.StudentId == studentId && item.SubjectId == subjectId,
            cancellationToken);

    private void ApplyUpdate(StudentEnrollment enrollment, Guid classId, byte[] expectedVersion)
    {
        db.Entry(enrollment).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        enrollment.ClassId = classId;
        enrollment.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<EnrollmentCommandResult<EnrollmentSummary>?> SaveStudentAsync(
        StudentEnrollment enrollment,
        Guid studentId,
        Guid subjectId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAndClearAsync(transaction, cancellationToken);
            return EnrollmentCommandResult<EnrollmentSummary>.Conflict(
                "stale_enrollment",
                CatalogConstants.StaleEnrollment,
                await GetStudentAsync(studentId, subjectId, cancellationToken));
        }
        catch (DbUpdateException exception) when (IsConstraintConflict(exception))
        {
            await RollbackAndClearAsync(transaction, cancellationToken);
            return EnrollmentCommandResult<EnrollmentSummary>.Conflict(
                "enrollment_conflict",
                CatalogConstants.EnrollmentConflict,
                await GetStudentAsync(studentId, subjectId, cancellationToken));
        }
    }

    private async Task<EnrollmentCommandResult<AdminEnrollmentSummary>?> SaveAdminAsync(
        Guid studentId,
        Guid subjectId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAndClearAsync(transaction, cancellationToken);
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Conflict(
                "stale_enrollment",
                CatalogConstants.StaleEnrollment,
                await GetAdminAsync(studentId, subjectId, cancellationToken));
        }
        catch (DbUpdateException exception) when (IsConstraintConflict(exception))
        {
            await RollbackAndClearAsync(transaction, cancellationToken);
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Conflict(
                "enrollment_conflict",
                CatalogConstants.EnrollmentDataChanged,
                await GetAdminAsync(studentId, subjectId, cancellationToken));
        }
    }

    /// <summary>On failure, rolls back then clears the change tracker so this <see cref="CatalogDbContext"/> instance stays
    /// safe for a subsequent call in the same request scope.</summary>
    private async Task RollbackAndClearAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static bool TryDecodeRowVersion(string? encoded, out byte[]? rowVersion)
    {
        if (encoded is null)
        {
            rowVersion = null;
            return true;
        }

        try
        {
            rowVersion = Convert.FromBase64String(encoded);
            return rowVersion.Length == 8;
        }
        catch (FormatException)
        {
            rowVersion = null;
            return false;
        }
    }

    private static bool IsConstraintConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 or 547 };

    private async Task<EnrollmentSummary?> GetStudentAsync(Guid studentId, Guid subjectId, CancellationToken cancellationToken)
    {
        var item = await ProjectStudent(db.StudentEnrollments.AsNoTracking()
                .Where(entry => entry.StudentId == studentId && entry.SubjectId == subjectId))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToSummary(item);
    }

    private async Task<EnrollmentSummary?> GetStudentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var item = await ProjectStudent(db.StudentEnrollments.AsNoTracking().Where(entry => entry.Id == enrollmentId))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToSummary(item);
    }

    private async Task<AdminEnrollmentSummary?> GetAdminAsync(Guid studentId, Guid subjectId, CancellationToken cancellationToken)
    {
        var item = await ProjectAdmin(db.StudentEnrollments.AsNoTracking()
                .Where(entry => entry.StudentId == studentId && entry.SubjectId == subjectId))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToSummary(item);
    }

    private static IQueryable<EnrollmentProjection> ProjectStudent(IQueryable<StudentEnrollment> query) =>
        query.Select(item => new EnrollmentProjection(
            item.Id,
            item.SubjectId,
            item.Subject.Code,
            item.Subject.Name,
            item.Subject.RegistrationStatus,
            item.ClassId,
            item.Class.Name,
            item.RowVersion,
            item.CreatedAt,
            item.UpdatedAt));

    private static IQueryable<AdminEnrollmentProjection> ProjectAdmin(IQueryable<StudentEnrollment> query) =>
        query.Select(item => new AdminEnrollmentProjection(
            item.Id,
            item.StudentId,
            item.SubjectId,
            item.Subject.Code,
            item.Subject.Name,
            item.Subject.RegistrationStatus,
            item.ClassId,
            item.Class.Name,
            item.RowVersion,
            item.CreatedAt,
            item.UpdatedAt));

    private static EnrollmentSummary ToSummary(EnrollmentProjection item) => new(
        item.Id,
        item.SubjectId,
        item.SubjectCode,
        item.SubjectName,
        item.RegistrationStatus,
        item.ClassId,
        item.ClassName,
        Convert.ToBase64String(item.RowVersion),
        item.CreatedAt,
        item.UpdatedAt);

    private static AdminEnrollmentSummary ToSummary(AdminEnrollmentProjection item) => new(
        item.Id,
        item.StudentId,
        item.SubjectId,
        item.SubjectCode,
        item.SubjectName,
        item.RegistrationStatus,
        item.ClassId,
        item.ClassName,
        Convert.ToBase64String(item.RowVersion),
        item.CreatedAt,
        item.UpdatedAt);

    private sealed record EnrollmentProjection(
        Guid Id,
        Guid SubjectId,
        string SubjectCode,
        string SubjectName,
        RegistrationStatus RegistrationStatus,
        Guid ClassId,
        string ClassName,
        byte[] RowVersion,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record AdminEnrollmentProjection(
        Guid Id,
        Guid StudentId,
        Guid SubjectId,
        string SubjectCode,
        string SubjectName,
        RegistrationStatus RegistrationStatus,
        Guid ClassId,
        string ClassName,
        byte[] RowVersion,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
