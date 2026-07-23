namespace AutoGrading.Identity.Api.Interfaces;

/// <summary>Auth-framework-free view of the caller, built by the endpoint from <c>ClaimsPrincipal</c>
/// before calling into <c>Service/</c> — keeps the service free of ASP.NET Core auth types.</summary>
public sealed record RequesterContext(Guid? UserId, bool IsStudent, bool IsLecturer, bool IsAdmin);
