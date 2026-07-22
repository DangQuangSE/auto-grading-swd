namespace AutoGrading.Catalog.Api.Endpoints;

internal static class EnrollmentHttpResults
{
    public static IResult From<T>(EnrollmentCommandResult<T> result) => result.Status switch
    {
        EnrollmentCommandStatus.Success => Results.Ok(result.Value),
        EnrollmentCommandStatus.Invalid => Results.BadRequest(new { code = result.Code, message = result.Message }),
        EnrollmentCommandStatus.NotFound => Results.NotFound(new { code = result.Code, message = result.Message }),
        EnrollmentCommandStatus.Conflict => Results.Conflict(new
        {
            code = result.Code,
            message = result.Message,
            current = result.Current
        }),
        _ => throw new InvalidOperationException($"Unsupported enrollment result status: {result.Status}.")
    };
}
