using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Service;

/// <summary>Mostly a pass-through to <see cref="IEnrollmentRepository"/> — see <see cref="IEnrollmentService"/>'s XML
/// doc for why the upsert/correct methods return <see cref="EnrollmentCommandResult{T}"/> rather than throwing.</summary>
public sealed class EnrollmentService(IEnrollmentRepository repo) : IEnrollmentService
{
    public Task<PagedResult<EnrollmentSummary>> ListStudentAsync(Guid studentId, int? page, int? pageSize, CancellationToken cancellationToken) =>
        repo.ListStudentAsync(studentId, page, pageSize, cancellationToken);

    public Task<PagedResult<AdminEnrollmentSummary>> ListAdminAsync(
        Guid? studentId,
        Guid? subjectId,
        Guid? classId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
        repo.ListAdminAsync(studentId, subjectId, classId, page, pageSize, cancellationToken);

    public Task<List<Guid>> ListStudentIdsForLecturerAsync(Guid lecturerId, Guid subjectId, CancellationToken cancellationToken) =>
        repo.ListStudentIdsForLecturerAsync(lecturerId, subjectId, cancellationToken);

    public Task<EnrollmentCommandResult<EnrollmentSummary>> UpsertStudentAsync(
        Guid studentId,
        Guid subjectId,
        Guid classId,
        string? rowVersion,
        CancellationToken cancellationToken) =>
        repo.UpsertStudentAsync(studentId, subjectId, classId, rowVersion, cancellationToken);

    public Task<EnrollmentCommandResult<AdminEnrollmentSummary>> CorrectAdminAsync(
        Guid studentId,
        Guid subjectId,
        Guid classId,
        string? rowVersion,
        CancellationToken cancellationToken) =>
        repo.CorrectAdminAsync(studentId, subjectId, classId, rowVersion, cancellationToken);
}
