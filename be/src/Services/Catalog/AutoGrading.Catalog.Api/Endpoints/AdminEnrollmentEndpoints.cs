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
        EnrollmentQueries queries,
        CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListAdminAsync(
            page,
            pageSize,
            studentId,
            subjectId,
            classId,
            cancellationToken));

    private static async Task<IResult> CorrectAsync(
        Guid studentId,
        Guid subjectId,
        UpsertEnrollmentRequest request,
        EnrollmentCommands commands,
        CancellationToken cancellationToken) =>
        EnrollmentHttpResults.From(
            await commands.CorrectAdminAsync(studentId, subjectId, request, cancellationToken));
}
