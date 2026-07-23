using System.Security.Claims;
using AutoGrading.Catalog.Api.Interfaces;

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
        IEnrollmentService service,
        CancellationToken cancellationToken)
    {
        return TryGetStudentId(caller, out var studentId)
            ? Results.Ok(await service.ListStudentAsync(studentId, page, pageSize, cancellationToken))
            : Results.Unauthorized();
    }

    private static async Task<IResult> UpsertMineAsync(
        Guid subjectId,
        UpsertEnrollmentRequest request,
        ClaimsPrincipal caller,
        IEnrollmentService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetStudentId(caller, out var studentId))
        {
            return Results.Unauthorized();
        }

        return EnrollmentHttpResults.From(
            await service.UpsertStudentAsync(studentId, subjectId, request.ClassId, request.RowVersion, cancellationToken));
    }

    private static bool TryGetStudentId(ClaimsPrincipal caller, out Guid studentId) =>
        Guid.TryParse(caller.FindFirstValue(ClaimTypes.NameIdentifier), out studentId) && studentId != Guid.Empty;
}
