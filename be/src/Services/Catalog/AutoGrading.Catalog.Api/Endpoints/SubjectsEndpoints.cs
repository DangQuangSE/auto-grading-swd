using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Contracts.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class SubjectsEndpoints
{
    public static IEndpointRouteBuilder MapSubjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/subjects").WithTags("Subjects");

        group.MapGet("/", async (int? page, int? pageSize, string? search, CatalogDbContext db, CancellationToken ct) =>
            {
                var (normalizedPage, normalizedPageSize) = PaginationDefaults.Normalize(page, pageSize);

                var query = db.Subjects.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(s => s.Code.Contains(search) || s.Name.Contains(search));
                }

                var totalCount = await query.CountAsync(ct);
                var items = await query
                    .OrderBy(s => s.Code)
                    .Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToListAsync(ct);

                return Results.Ok(new PagedResult<Subject>(items, normalizedPage, normalizedPageSize, totalCount));
            })
            .RequireAuthorization();

        group.MapPost("/", async (CreateSubjectRequest request, CatalogDbContext db, CancellationToken ct) =>
            {
                var subject = new Subject { Code = request.Code, Name = request.Name };
                db.Subjects.Add(subject);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/subjects/{subject.Id}", subject);
            })
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }
}

public sealed record CreateSubjectRequest(string Code, string Name);
