using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

public interface IEnrollmentRepository
{
    Task<PagedResult<EnrollmentSummary>> ListStudentAsync(Guid studentId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<PagedResult<AdminEnrollmentSummary>> ListAdminAsync(Guid? studentId, Guid? subjectId, Guid? classId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<List<Guid>> ListStudentIdsForLecturerAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>Byte-for-byte port of <c>EnrollmentCommands.UpsertStudentAsync</c>: the entire Serializable transaction,
    /// row-version optimistic-concurrency check, and constraint-conflict handling (SQL error 2601/2627/547) live inside this
    /// one method — never split across Service/Endpoints, which would reopen the race condition Serializable isolation
    /// prevents. On failure the transaction is rolled back and <c>ChangeTracker.Clear()</c> is called before returning, so
    /// this <see cref="Data.CatalogDbContext"/> instance stays safe for a subsequent call in the same request scope.
    /// Queries <c>Class</c> directly via its own DbContext reference (never via <see cref="IClassRepository"/>) to keep the
    /// whole check-then-write sequence inside one transaction.</summary>
    Task<EnrollmentCommandResult<EnrollmentSummary>> UpsertStudentAsync(Guid studentId, Guid subjectId, Guid classId, string? rowVersion, CancellationToken cancellationToken);

    /// <summary>Byte-for-byte port of <c>EnrollmentCommands.CorrectAdminAsync</c>; same transaction/rollback/cleanup guarantees as <see cref="UpsertStudentAsync"/>.</summary>
    Task<EnrollmentCommandResult<AdminEnrollmentSummary>> CorrectAdminAsync(Guid studentId, Guid subjectId, Guid classId, string? rowVersion, CancellationToken cancellationToken);
}
