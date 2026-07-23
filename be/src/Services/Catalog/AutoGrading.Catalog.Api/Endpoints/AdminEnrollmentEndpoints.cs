using AutoGrading.Catalog.Api.Interfaces;

namespace AutoGrading.Catalog.Api.Endpoints;

internal static class AdminEnrollmentEndpoints
{
    public static RouteGroupBuilder MapAdminEnrollmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin", ListAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));
        group.MapPut("/admin/{studentId:guid}/{subjectId:guid}", CorrectAsync)
            .RequireAuthorization(policy => policy.RequireRole("admin"));
        return group;
    }

    private static async Task<IResult> ListAsync(
        int? page,
        int? pageSize,
        Guid? studentId,
        Guid? subjectId,
        Guid? classId,
        IEnrollmentService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListAdminAsync(
            studentId,
            subjectId,
            classId,
            page,
            pageSize,
            cancellationToken));

    private static async Task<IResult> CorrectAsync(
        Guid studentId,
        Guid subjectId,
        UpsertEnrollmentRequest request,
        IEnrollmentService service,
        CancellationToken cancellationToken) =>
        EnrollmentHttpResults.From(
            await service.CorrectAdminAsync(studentId, subjectId, request.ClassId, request.RowVersion, cancellationToken));
}
