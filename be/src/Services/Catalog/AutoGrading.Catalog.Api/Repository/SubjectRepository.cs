using System.Data;
using AutoGrading.Catalog.Api.Constant;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Repository;

public sealed class SubjectRepository(CatalogDbContext db) : ISubjectRepository
{
    public Task<PagedResult<Subject>> ListAsync(string? search, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Subjects.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(subject => subject.Code.Contains(term) || subject.Name.Contains(term));
        }

        return ToPagedResultAsync(query, normalizedPage, normalizedPageSize, cancellationToken);
    }

    public Task<PagedResult<Subject>> ListOpenAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Subjects.AsNoTracking().Where(subject => subject.RegistrationStatus == RegistrationStatus.Open);

        return ToPagedResultAsync(query, normalizedPage, normalizedPageSize, cancellationToken);
    }

    public Task<Subject?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Subjects.AsNoTracking().FirstOrDefaultAsync(subject => subject.Id == id, cancellationToken);

    public Task<bool> AnyAsync(Guid id, CancellationToken cancellationToken) =>
        db.Subjects.AnyAsync(subject => subject.Id == id, cancellationToken);

    public async Task<Subject> CreateAsync(Subject subject, CancellationToken cancellationToken)
    {
        db.Subjects.Add(subject);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new CatalogConflictException("subject_code_exists", CatalogConstants.SubjectCodeExists);
        }

        return subject;
    }

    public async Task<Subject?> UpdateRegistrationAsync(Guid id, RegistrationStatus status, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var subject = await db.Subjects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject is null)
        {
            return null;
        }

        subject.RegistrationStatus = status;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return subject;
    }

    private static async Task<PagedResult<Subject>> ToPagedResultAsync(
        IQueryable<Subject> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(subject => subject.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Subject>(items, page, pageSize, totalCount);
    }
}
