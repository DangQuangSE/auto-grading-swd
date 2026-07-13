using System.Security.Claims;
using AutoGrading.Common.Auth;
using AutoGrading.Identity.Api.Data;
using AutoGrading.Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Authorization;

public enum RosterAuthorizationResult
{
    Admin,
    ClassLecturer,
    Grader,
    Denied,
}

/// <summary>Determines whether a caller may edit a target student's roster fields (StudentCode/ClassId).
/// Reused by Phase 5's bulk import to report a specific per-row skip reason instead of a generic denial.</summary>
public static class RosterAuthorization
{
    public static async Task<RosterAuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal caller,
        User target,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        if (caller.IsInRole("admin"))
        {
            return RosterAuthorizationResult.Admin;
        }

        if (!caller.IsInRole("lecturer"))
        {
            return RosterAuthorizationResult.Denied;
        }

        var callerId = caller.GetUserId();

        if (target.ClassId is { } classId)
        {
            var isClassLecturer = await db.ClassLecturerCaches
                .AnyAsync(c => c.ClassId == classId && c.LecturerId == callerId, cancellationToken);
            if (isClassLecturer)
            {
                return RosterAuthorizationResult.ClassLecturer;
            }
        }

        var isGrader = await db.SubmissionGraders
            .Join(db.SubmissionStudents, g => g.SubmissionId, s => s.SubmissionId, (g, s) => new { s.StudentId, g.LecturerId })
            .AnyAsync(x => x.StudentId == target.Id && x.LecturerId == callerId, cancellationToken);

        return isGrader ? RosterAuthorizationResult.Grader : RosterAuthorizationResult.Denied;
    }
}
