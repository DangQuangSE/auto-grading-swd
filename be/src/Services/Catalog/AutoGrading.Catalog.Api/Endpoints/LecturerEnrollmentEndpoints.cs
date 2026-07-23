using System.Security.Claims;
using AutoGrading.Catalog.Api.Interfaces;

namespace AutoGrading.Catalog.Api.Endpoints;

/// <summary>Lets a lecturer (or a trusted service caller acting on a lecturer's behalf) list the
/// student ids enrolled in any class that lecturer teaches for a given subject — a lecturer can
/// teach several classes of the same subject, so this is a set union across all of them, not a
/// single class lookup.</summary>
internal static class LecturerEnrollmentEndpoints
{
    public static RouteGroupBuilder MapLecturerEnrollmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/lecturer-student-ids", ListForLecturerAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "service"));
        return group;
    }

    private static async Task<IResult> ListForLecturerAsync(
        Guid subjectId,
        Guid? lecturerId,
        ClaimsPrincipal caller,
        IEnrollmentService service,
        CancellationToken cancellationToken)
    {
        Guid effectiveLecturerId;
        if (caller.IsInRole("service"))
        {
            if (lecturerId is not { } id || id == Guid.Empty)
            {
                return Results.BadRequest(new { error = "lecturerId is required when called by a service." });
            }

            effectiveLecturerId = id;
        }
        else if (Guid.TryParse(caller.FindFirstValue(ClaimTypes.NameIdentifier), out var callerId))
        {
            effectiveLecturerId = callerId;
        }
        else
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await service.ListStudentIdsForLecturerAsync(effectiveLecturerId, subjectId, cancellationToken));
    }
}
