namespace AutoGrading.Catalog.Api.Endpoints;

public static class EnrollmentsEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/enrollments").WithTags("Enrollments");
        group.MapStudentEnrollmentEndpoints();
        group.MapAdminEnrollmentEndpoints();
        group.MapLecturerEnrollmentEndpoints();
        return app;
    }
}
