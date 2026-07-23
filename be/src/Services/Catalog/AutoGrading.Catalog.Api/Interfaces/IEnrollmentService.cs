using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Interfaces;

/// <summary>Mostly a pass-through to <see cref="IEnrollmentRepository"/> — the upsert/correct methods return
/// <see cref="EnrollmentCommandResult{T}"/> directly rather than throwing, per the plan's deliberate exception to the
/// exception-based-signaling convention used elsewhere in this service (see plan.md design decision #4).</summary>
public interface IEnrollmentService
{
    Task<PagedResult<EnrollmentSummary>> ListStudentAsync(Guid studentId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<PagedResult<AdminEnrollmentSummary>> ListAdminAsync(Guid? studentId, Guid? subjectId, Guid? classId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<List<Guid>> ListStudentIdsForLecturerAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken);

    Task<EnrollmentCommandResult<EnrollmentSummary>> UpsertStudentAsync(Guid studentId, Guid subjectId, Guid classId, string? rowVersion, CancellationToken cancellationToken);

    Task<EnrollmentCommandResult<AdminEnrollmentSummary>> CorrectAdminAsync(Guid studentId, Guid subjectId, Guid classId, string? rowVersion, CancellationToken cancellationToken);
}
