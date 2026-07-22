using AutoGrading.Catalog.Api.Data;
using AutoGrading.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Endpoints;

internal sealed class EnrollmentQueries(CatalogDbContext db)
{
    public async Task<PagedResult<EnrollmentSummary>> ListStudentAsync(
        Guid studentId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.StudentEnrollments.AsNoTracking().Where(item => item.StudentId == studentId);
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(normalizedPage - 1) * normalizedPageSize;
        if (offset >= totalCount)
        {
            return new(Array.Empty<EnrollmentSummary>(), normalizedPage, normalizedPageSize, totalCount);
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

        return new(items.Select(EnrollmentSummary.From).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<PagedResult<AdminEnrollmentSummary>> ListAdminAsync(
        int? page,
        int? pageSize,
        Guid? studentId,
        Guid? subjectId,
        Guid? classId,
        CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.StudentEnrollments.AsNoTracking();
        if (studentId.HasValue) query = query.Where(item => item.StudentId == studentId.Value);
        if (subjectId.HasValue) query = query.Where(item => item.SubjectId == subjectId.Value);
        if (classId.HasValue) query = query.Where(item => item.ClassId == classId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(normalizedPage - 1) * normalizedPageSize;
        if (offset >= totalCount)
        {
            return new(Array.Empty<AdminEnrollmentSummary>(), normalizedPage, normalizedPageSize, totalCount);
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

        return new(items.Select(AdminEnrollmentSummary.From).ToList(), normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<EnrollmentSummary?> GetStudentAsync(
        Guid studentId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var item = await ProjectStudent(db.StudentEnrollments.AsNoTracking()
            .Where(item => item.StudentId == studentId && item.SubjectId == subjectId))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : EnrollmentSummary.From(item);
    }

    public async Task<EnrollmentSummary?> GetStudentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var item = await ProjectStudent(db.StudentEnrollments.AsNoTracking().Where(item => item.Id == enrollmentId))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : EnrollmentSummary.From(item);
    }

    public async Task<AdminEnrollmentSummary?> GetAdminAsync(
        Guid studentId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var item = await ProjectAdmin(db.StudentEnrollments.AsNoTracking()
            .Where(item => item.StudentId == studentId && item.SubjectId == subjectId))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : AdminEnrollmentSummary.From(item);
    }

    private static IQueryable<EnrollmentProjection> ProjectStudent(IQueryable<Domain.StudentEnrollment> query) =>
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

    private static IQueryable<AdminEnrollmentProjection> ProjectAdmin(IQueryable<Domain.StudentEnrollment> query) =>
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
}
