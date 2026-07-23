using System.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Repository;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AutoGrading.Catalog.Api.Endpoints;

internal sealed class EnrollmentCommands(CatalogDbContext db, EnrollmentQueries queries)
{
    public async Task<EnrollmentCommandResult<EnrollmentSummary>> UpsertStudentAsync(
        Guid studentId,
        Guid subjectId,
        UpsertEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty || request.ClassId == Guid.Empty)
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Invalid(
                "invalid_enrollment",
                "SubjectId and ClassId are required.");
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Invalid(
                "invalid_row_version",
                "RowVersion must be an 8-byte base64 value.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var subject = await db.Subjects.FirstOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
        if (subject is null)
        {
            return EnrollmentCommandResult<EnrollmentSummary>.NotFound(
                "subject_not_found",
                "Subject does not exist.");
        }

        if (subject.RegistrationStatus != RegistrationStatus.Open)
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Conflict(
                "registration_closed",
                "Subject registration is closed.");
        }

        if (!await ClassMatchesSubjectAsync(request.ClassId, subjectId, cancellationToken))
        {
            return EnrollmentCommandResult<EnrollmentSummary>.Invalid(
                "class_subject_mismatch",
                "Class does not belong to the subject.");
        }

        var enrollment = await FindEnrollmentAsync(studentId, subjectId, cancellationToken);
        if (enrollment is not null && enrollment.ClassId == request.ClassId)
        {
            await transaction.CommitAsync(cancellationToken);
            return EnrollmentCommandResult<EnrollmentSummary>.Success(
                await queries.GetStudentByIdAsync(enrollment.Id, cancellationToken));
        }

        if (enrollment is null)
        {
            if (expectedVersion is not null)
            {
                return EnrollmentCommandResult<EnrollmentSummary>.Conflict(
                    "enrollment_missing",
                    "Enrollment no longer exists. Refresh and retry.");
            }

            enrollment = new StudentEnrollment
            {
                StudentId = studentId,
                SubjectId = subjectId,
                ClassId = request.ClassId
            };
            db.StudentEnrollments.Add(enrollment);
        }
        else
        {
            if (expectedVersion is null)
            {
                return EnrollmentCommandResult<EnrollmentSummary>.Conflict(
                    "row_version_required",
                    "Refresh the enrollment before changing it.",
                    await queries.GetStudentAsync(studentId, subjectId, cancellationToken));
            }

            ApplyUpdate(enrollment, request.ClassId, expectedVersion);
        }

        var failure = await SaveStudentAsync(enrollment, studentId, subjectId, transaction, cancellationToken);
        return failure ?? EnrollmentCommandResult<EnrollmentSummary>.Success(
            await queries.GetStudentByIdAsync(enrollment.Id, cancellationToken));
    }

    public async Task<EnrollmentCommandResult<AdminEnrollmentSummary>> CorrectAdminAsync(
        Guid studentId,
        Guid subjectId,
        UpsertEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty || subjectId == Guid.Empty || request.ClassId == Guid.Empty)
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Invalid(
                "invalid_enrollment",
                "StudentId, SubjectId and ClassId are required.");
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var expectedVersion))
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Invalid(
                "invalid_row_version",
                "RowVersion must be an 8-byte base64 value.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (!await db.Subjects.AnyAsync(item => item.Id == subjectId, cancellationToken))
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.NotFound(
                "subject_not_found",
                "Subject does not exist.");
        }

        if (!await ClassMatchesSubjectAsync(request.ClassId, subjectId, cancellationToken))
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Invalid(
                "class_subject_mismatch",
                "Class does not belong to the subject.");
        }

        var enrollment = await FindEnrollmentAsync(studentId, subjectId, cancellationToken);
        if (enrollment is null)
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.NotFound(
                "enrollment_not_found",
                "Enrollment does not exist.");
        }

        if (enrollment.ClassId == request.ClassId)
        {
            await transaction.CommitAsync(cancellationToken);
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Success(
                await queries.GetAdminAsync(studentId, subjectId, cancellationToken));
        }

        if (expectedVersion is null)
        {
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Conflict(
                "row_version_required",
                "Refresh the enrollment before changing it.",
                await queries.GetAdminAsync(studentId, subjectId, cancellationToken));
        }

        ApplyUpdate(enrollment, request.ClassId, expectedVersion);
        var failure = await SaveAdminAsync(studentId, subjectId, transaction, cancellationToken);
        return failure ?? EnrollmentCommandResult<AdminEnrollmentSummary>.Success(
            await queries.GetAdminAsync(studentId, subjectId, cancellationToken));
    }

    private Task<bool> ClassMatchesSubjectAsync(Guid classId, Guid subjectId, CancellationToken cancellationToken) =>
        db.Classes.AnyAsync(item => item.Id == classId && item.SubjectId == subjectId, cancellationToken);

    private Task<StudentEnrollment?> FindEnrollmentAsync(
        Guid studentId,
        Guid subjectId,
        CancellationToken cancellationToken) =>
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
                "Enrollment changed. Refresh and retry.",
                await queries.GetStudentAsync(studentId, subjectId, cancellationToken));
        }
        catch (DbUpdateException exception) when (IsConstraintConflict(exception))
        {
            await RollbackAndClearAsync(transaction, cancellationToken);
            return EnrollmentCommandResult<EnrollmentSummary>.Conflict(
                "enrollment_conflict",
                "Enrollment could not be saved because the data changed.",
                await queries.GetStudentAsync(studentId, subjectId, cancellationToken));
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
                "Enrollment changed. Refresh and retry.",
                await queries.GetAdminAsync(studentId, subjectId, cancellationToken));
        }
        catch (DbUpdateException exception) when (IsConstraintConflict(exception))
        {
            await RollbackAndClearAsync(transaction, cancellationToken);
            return EnrollmentCommandResult<AdminEnrollmentSummary>.Conflict(
                "enrollment_conflict",
                "Enrollment data changed. Refresh and retry.",
                await queries.GetAdminAsync(studentId, subjectId, cancellationToken));
        }
    }

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
}
