using AutoGrading.Catalog.Api.Constant;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Service;

public sealed class AssignmentService(IAssignmentRepository repo) : IAssignmentService
{
    public Task<PagedResult<Assignment>> ListAsync(Guid? subjectId, int? page, int? pageSize, CancellationToken cancellationToken) =>
        repo.ListAsync(subjectId, page, pageSize, cancellationToken);

    public Task<Assignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        repo.GetByIdAsync(id, cancellationToken);

    public Task<Assignment> CreateAsync(
        Guid subjectId,
        string title,
        string? description,
        DateTimeOffset? dueDate,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        ValidateMaxAttempts(maxAttempts);

        var assignment = new Assignment
        {
            SubjectId = subjectId,
            Title = title,
            Description = description,
            DueDate = dueDate,
            MaxAttempts = maxAttempts,
        };

        return repo.CreateAsync(assignment, cancellationToken);
    }

    public Task<Assignment?> UpdateAsync(
        Guid id,
        string title,
        string? description,
        DateTimeOffset? dueDate,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        ValidateMaxAttempts(maxAttempts);

        return repo.UpdateAsync(id, title, description, dueDate, maxAttempts, cancellationToken);
    }

    private static void ValidateMaxAttempts(int maxAttempts)
    {
        if (maxAttempts < 1)
        {
            throw new CatalogValidationException("invalid_max_attempts", CatalogConstants.InvalidMaxAttempts);
        }
    }
}
