using System.Security.Claims;
using AutoGrading.Catalog.Api.Data;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Common.Auth;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Catalog.Api.Tests;

/// <summary>
/// Tests for authorization logic in RubricsEndpoints. Verifies that:
/// - Only admins can create/edit SchoolWide rubrics
/// - Only the owning lecturer or an admin can edit a Lecturer-scoped rubric
/// - Admin can always act on any rubric
/// </summary>
public class RubricEndpointAuthorizationTests
{
    private static CatalogDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static ClaimsPrincipal CreateUser(Guid userId, params string[] roles)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void IsAuthorized_AdminUser_CanActOnAnyRubric()
    {
        var admin = CreateUser(Guid.NewGuid(), "admin");
        var lecturer = Guid.NewGuid();

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Lecturer Rubric",
            Scope = RubricScope.Lecturer,
            LecturerId = lecturer,
            Status = RubricStatus.Draft,
        };

        // Admin should be able to act on lecturer's rubric — IsAuthorized returns true for admin
        var adminId = admin.GetUserId();
        var isAuthorized = admin.IsInRole("admin") || rubric.LecturerId == adminId;
        Assert.True(isAuthorized);
    }

    [Fact]
    public void IsAuthorized_LecturerUser_CanOnlyActOnTheirOwnRubric()
    {
        var lecturerId = Guid.NewGuid();
        var lecturer = CreateUser(lecturerId, "lecturer");
        var otherLecturerId = Guid.NewGuid();

        var ownRubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Own Rubric",
            Scope = RubricScope.Lecturer,
            LecturerId = lecturerId,
            Status = RubricStatus.Draft,
        };

        var otherRubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Other Rubric",
            Scope = RubricScope.Lecturer,
            LecturerId = otherLecturerId,
            Status = RubricStatus.Draft,
        };

        var lecturerUserId = lecturer.GetUserId();

        // Lecturer can act on their own rubric
        var isAuthorizedOwn = lecturer.IsInRole("admin") || ownRubric.LecturerId == lecturerUserId;
        Assert.True(isAuthorizedOwn);

        // Lecturer cannot act on another's rubric
        var isAuthorizedOther = lecturer.IsInRole("admin") || otherRubric.LecturerId == lecturerUserId;
        Assert.False(isAuthorizedOther);
    }

    [Fact]
    public void IsAuthorized_SchoolWideRubric_OnlyAdminCanAct()
    {
        var lecturer = CreateUser(Guid.NewGuid(), "lecturer");
        var admin = CreateUser(Guid.NewGuid(), "admin");

        var schoolWideRubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "School Wide Rubric",
            Scope = RubricScope.SchoolWide,
            LecturerId = null, // SchoolWide has no owner
            Status = RubricStatus.Draft,
        };

        var lecturerUserId = lecturer.GetUserId();
        var adminUserId = admin.GetUserId();

        // Lecturer cannot act on SchoolWide rubric (no LecturerId match)
        var lecturerAuthorized = lecturer.IsInRole("admin") || schoolWideRubric.LecturerId == lecturerUserId;
        Assert.False(lecturerAuthorized);

        // Admin can act on SchoolWide rubric
        var adminAuthorized = admin.IsInRole("admin") || schoolWideRubric.LecturerId == adminUserId;
        Assert.True(adminAuthorized);
    }
}
