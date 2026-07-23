using AutoGrading.Catalog.Api.Constant;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Repository;

public sealed class ClassRepository(CatalogDbContext db, IEventBus eventBus) : IClassRepository
{
    public Task<List<Class>> ListAsync(CancellationToken cancellationToken) =>
        db.Classes.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<PagedResult<Class>> ListAdminAsync(Guid? subjectId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Classes.AsNoTracking().Include(item => item.Subject).AsQueryable();
        if (subjectId.HasValue)
        {
            query = query.Where(item => item.SubjectId == subjectId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Class>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<PagedResult<Class>> ListForSubjectAsync(Guid subjectId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);
        var query = db.Classes.AsNoTracking().Where(item => item.SubjectId == subjectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Class>(items, normalizedPage, normalizedPageSize, totalCount);
    }

    public Task<Class?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Classes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<Class> CreateAsync(Class newClass, CancellationToken cancellationToken)
    {
        db.Classes.Add(newClass);
        return SaveAndPublishAsync(newClass, cancellationToken);
    }

    public Task<Class> UpdateAsync(Class updatedClass, CancellationToken cancellationToken) =>
        SaveAndPublishAsync(updatedClass, cancellationToken);

    public Task<bool> AnyAsync(Guid id, CancellationToken cancellationToken) =>
        db.Classes.AnyAsync(item => item.Id == id, cancellationToken);

    public Task<bool> AnyWithEnrollmentsAsync(Guid classId, CancellationToken cancellationToken) =>
        db.StudentEnrollments.AnyAsync(enrollment => enrollment.ClassId == classId, cancellationToken);

    /// <summary>Saves the class and publishes <c>ClassLecturerAssigned</c> inside one transaction, matching the original
    /// endpoint's <c>SaveAndPublishAsync</c> exactly: a constraint-violation on save maps to <see cref="CatalogConflictException"/>;
    /// any other failure (including the event publish itself) maps to <see cref="ClassEventPublishException"/>. Both roll back first.</summary>
    private async Task<Class> SaveAndPublishAsync(Class @class, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await eventBus.PublishAsync(
                new ClassLecturerAssigned(@class.Id, @class.Name, @class.LecturerId),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return @class;
        }
        catch (DbUpdateException exception) when (exception.InnerException is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new CatalogConflictException("class_conflict", CatalogConstants.ClassConflict);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ClassEventPublishException(CatalogConstants.ClassEventPublishFailed);
        }
    }
}
