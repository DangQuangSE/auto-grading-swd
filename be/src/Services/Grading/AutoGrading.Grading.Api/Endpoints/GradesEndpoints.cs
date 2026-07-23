using System.Security.Claims;
using AutoGrading.Grading.Api.Dto;
using AutoGrading.Grading.Api.Interfaces;

namespace AutoGrading.Grading.Api.Endpoints;

public static class GradesEndpoints
{
    public static IEndpointRouteBuilder MapGradesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/grades").WithTags("Grades");

        // AI output is review material. It is never exposed directly to students.
        group.MapGet("/{submissionId:guid}/runs", GetRunsAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        // Student-safe projection: only the run selected by a publication is returned.
        group.MapGet("/{submissionId:guid}/result", GetPublishedResultAsync)
            .RequireAuthorization();

        group.MapGet("/{submissionId:guid}/final", GetFinalGradeAsync)
            .RequireAuthorization();

        group.MapGet("/final", GetFinalGradesBatchAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/{submissionId:guid}/publish", PublishGradeAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/publish-all", PublishAllAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));

        group.MapPost("/{submissionId:guid}/regrade", RegradeAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }

    /// <summary>Builds the auth-framework-free <see cref="RequesterContext"/> for the service layer.
    /// A student whose <c>NameIdentifier</c> claim is missing/not a Guid is rejected here with
    /// <c>Forbid()</c> before the service is ever called.</summary>
    private static bool TryBuildRequesterContext(ClaimsPrincipal user, out RequesterContext requester, out IResult? forbidResult)
    {
        var isStudent = user.IsInRole("student");
        var isLecturer = user.IsInRole("lecturer");
        var isAdmin = user.IsInRole("admin");
        Guid? userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;

        if (isStudent && userId is null)
        {
            requester = null!;
            forbidResult = Results.Forbid();
            return false;
        }

        requester = new RequesterContext(userId, isStudent, isLecturer, isAdmin);
        forbidResult = null;
        return true;
    }

    private static async Task<IResult> GetRunsAsync(Guid submissionId, ClaimsPrincipal user, IGradingService service, CancellationToken ct)
    {
        if (!TryBuildRequesterContext(user, out var requester, out var forbid)) return forbid!;

        try
        {
            var runs = await service.GetRunsForRequesterAsync(submissionId, requester, ct);
            return Results.Ok(runs.Select(AiGradingRunResponse.FromDomain));
        }
        catch (GradingForbiddenException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> GetPublishedResultAsync(Guid submissionId, ClaimsPrincipal user, IGradingService service, CancellationToken ct)
    {
        if (!TryBuildRequesterContext(user, out var requester, out var forbid)) return forbid!;

        try
        {
            var result = await service.GetPublishedResultForRequesterAsync(submissionId, requester, ct);
            if (result.Grade is null)
            {
                return result.GradingDone is bool done ? Results.NotFound(new { gradingDone = done }) : Results.NotFound();
            }

            var run = result.Run is null ? null : AiGradingRunResponse.FromDomain(result.Run);
            return Results.Ok(new PublishedGradeResult(FinalGradeDetailResponse.FromDomain(result.Grade), result.PublishedAt!.Value, run));
        }
        catch (GradingForbiddenException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> GetFinalGradeAsync(Guid submissionId, ClaimsPrincipal user, IGradingService service, CancellationToken ct)
    {
        if (!TryBuildRequesterContext(user, out var requester, out var forbid)) return forbid!;

        try
        {
            var finalGrade = await service.GetFinalGradeForRequesterAsync(submissionId, requester, ct);
            return finalGrade is null ? Results.NotFound() : Results.Ok(FinalGradeDetailResponse.FromDomain(finalGrade));
        }
        catch (GradingForbiddenException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> GetFinalGradesBatchAsync(
        string[]? submissionIds, ClaimsPrincipal user, IGradingService service, CancellationToken ct)
    {
        if (!TryBuildRequesterContext(user, out var requester, out var forbid)) return forbid!;

        var ids = ParseIds(submissionIds);
        if (ids is null) return Results.Ok(Array.Empty<FinalGradeResponse>());

        var grades = await service.GetFinalGradesBatchForRequesterAsync(ids, requester, ct);
        return Results.Ok(grades.Select(FinalGradeResponse.FromData));
    }

    private static HashSet<Guid>? ParseIds(string[]? ids)
    {
        if (ids is not { Length: > 0 }) return null;
        var parsed = ids.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(v => Guid.TryParse(v, out _)).Select(Guid.Parse).ToHashSet();
        return parsed.Count > 0 ? parsed : null;
    }

    private static async Task<IResult> RegradeAsync(
        Guid submissionId, RegradeRequest request, ClaimsPrincipal user, IGradingService service, CancellationToken ct)
    {
        if (!TryBuildRequesterContext(user, out var requester, out var forbid)) return forbid!;

        try
        {
            await service.RegradeAsync(submissionId, request.AssignmentDescription, requester, ct);
            return Results.Accepted($"/grades/{submissionId}/runs");
        }
        catch (GradingForbiddenException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> PublishGradeAsync(
        Guid submissionId, PublishGradeRequest request, ClaimsPrincipal user, IGradingService service, CancellationToken ct)
    {
        if (!TryBuildRequesterContext(user, out var requester, out var forbid)) return forbid!;

        try
        {
            var grade = await service.PublishGradeAsync(submissionId, request.GradingRunId, request.FinalScore, request.Notes, requester, ct);
            return Results.Created($"/grades/{submissionId}/final", FinalGradeDetailResponse.FromDomain(grade));
        }
        catch (GradingForbiddenException)
        {
            return Results.Forbid();
        }
        catch (InvalidGradingRunException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> PublishAllAsync(ClaimsPrincipal user, IGradingService service, CancellationToken ct)
    {
        if (!TryBuildRequesterContext(user, out var requester, out var forbid)) return forbid!;

        try
        {
            var result = await service.PublishAllAsync(requester, ct);
            return Results.Ok(new PublishAllResponse(result.Published, result.Skipped, result.Failed));
        }
        catch (GradingForbiddenException)
        {
            return Results.Forbid();
        }
    }
}
