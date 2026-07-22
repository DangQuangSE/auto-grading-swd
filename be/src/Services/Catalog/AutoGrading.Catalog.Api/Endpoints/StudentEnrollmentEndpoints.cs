using System.Security.Claims;

namespace AutoGrading.Catalog.Api.Endpoints;

internal static class StudentEnrollmentEndpoints
{
    public static RouteGroupBuilder MapStudentEnrollmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", ListMineAsync)
            .RequireAuthorization(policy => policy.RequireRole("student"));
        group.MapPut("/me/{subjectId:guid}", UpsertMineAsync)
            .RequireAuthorization(policy => policy.RequireRole("student"));
        return group;
    }

    private static async Task<IResult> ListMineAsync(
        int? page,
        int? pageSize,
        ClaimsPrincipal caller,
        EnrollmentQueries queries,
        CancellationToken cancellationToken)
    {
        return TryGetStudentId(caller, out var studentId)
            ? Results.Ok(await queries.ListStudentAsync(studentId, page, pageSize, cancellationToken))
            : Results.Unauthorized();
    }

    private static async Task<IResult> UpsertMineAsync(
        Guid subjectId,
        UpsertEnrollmentRequest request,
        ClaimsPrincipal caller,
        EnrollmentCommands commands,
        CancellationToken cancellationToken)
    {
        if (!TryGetStudentId(caller, out var studentId))
        {
            return Results.Unauthorized();
        }

        return EnrollmentHttpResults.From(
            await commands.UpsertStudentAsync(studentId, subjectId, request, cancellationToken));
    }

    private static bool TryGetStudentId(ClaimsPrincipal caller, out Guid studentId) =>
        Guid.TryParse(caller.FindFirstValue(ClaimTypes.NameIdentifier), out studentId) && studentId != Guid.Empty;
}
