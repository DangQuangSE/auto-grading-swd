using System.Security.Claims;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Grading.Api.Endpoints;

public static class GradesEndpoints
{
    public static IEndpointRouteBuilder MapGradesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/grades").WithTags("Grades");

        group.MapGet("/{submissionId:guid}/runs", async (Guid submissionId, GradingDbContext db, CancellationToken ct) =>
            {
                var runs = await db.AiGradingRuns.AsNoTracking()
                    .Include(r => r.Scores)
                    .Where(r => r.SubmissionId == submissionId)
                    .ToListAsync(ct);

                return Results.Ok(runs);
            })
            .RequireAuthorization();

        group.MapGet("/{submissionId:guid}/final", async (Guid submissionId, GradingDbContext db, CancellationToken ct) =>
            {
                var finalGrade = await db.FinalGrades.AsNoTracking()
                    .Where(f => f.SubmissionId == submissionId)
                    .OrderByDescending(f => f.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                return finalGrade is null ? Results.NotFound() : Results.Ok(finalGrade);
            })
            .RequireAuthorization();

        group.MapPost("/{submissionId:guid}/publish", PublishGradeAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }

    private static async Task<IResult> PublishGradeAsync(
        Guid submissionId,
        PublishGradeRequest request,
        ClaimsPrincipal user,
        GradingDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var finalGrade = new FinalGrade
        {
            SubmissionId = submissionId,
            GradingRunId = request.GradingRunId,
            FinalScore = request.FinalScore,
            Notes = request.Notes,
            CreatedByUserId = userId,
        };
        db.FinalGrades.Add(finalGrade);

        var publication = new GradePublication
        {
            FinalGradeId = finalGrade.Id,
            SubmissionId = submissionId,
            PublishedByUserId = userId,
        };
        db.GradePublications.Add(publication);

        await db.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new GradePublished(submissionId, finalGrade.Id, finalGrade.FinalScore, userId),
            cancellationToken);

        return Results.Created($"/grades/{submissionId}/final", finalGrade);
    }
}

public sealed record PublishGradeRequest(Guid? GradingRunId, decimal FinalScore, string? Notes);
