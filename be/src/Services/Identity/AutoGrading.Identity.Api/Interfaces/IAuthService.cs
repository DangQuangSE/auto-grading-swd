using AutoGrading.Contracts.Enums;

namespace AutoGrading.Identity.Api.Interfaces;

public sealed record UserAuthResult(Guid UserId, string Email, string Role);

public sealed record AuthTokenResult(string Token, Guid UserId, string Email, string Role);

public interface IAuthService
{
    Task<UserAuthResult> RegisterAsync(string email, string password, string fullName, AppRole role, string? studentCode, Guid? classId, CancellationToken ct);

    Task<AuthTokenResult> LoginAsync(string email, string password, CancellationToken ct);

    Task<AuthTokenResult> GoogleLoginAsync(string idToken, CancellationToken ct);
}

/// <summary>Thrown by <c>AuthService.LoginAsync</c> when the email doesn't exist or the password is
/// invalid — combined into one exception matching today's behavior (endpoint returns 401 either way).</summary>
public sealed class InvalidCredentialsException() : Exception("Invalid email or password.");

public sealed class InvalidGoogleTokenException() : Exception("Invalid Google ID token.");

public sealed class EducationEmailNotVerifiedException() : Exception("Email is not verified or not an education domain.");
