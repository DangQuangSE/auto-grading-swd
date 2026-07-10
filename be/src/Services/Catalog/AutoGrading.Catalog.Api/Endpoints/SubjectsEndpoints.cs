using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class SubjectsEndpoints
{
    public static IEndpointRouteBuilder MapSubjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/subjects").WithTags("Subjects");

        group.MapGet("/", async (CatalogDbContext db, CancellationToken ct) =>
                Results.Ok(await db.Subjects.AsNoTracking().ToListAsync(ct)))
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
