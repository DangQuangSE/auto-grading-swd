using AutoGrading.Common.Auth;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Enums;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Auth;
using AutoGrading.Identity.Api.Data;
using AutoGrading.Identity.Api.Domain;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutoGrading.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/google", GoogleLoginAsync);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IdentityDbContext db,
        IPasswordHasher<User> passwordHasher,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Results.Conflict(new { message = "Email already registered." });
        }

        if (request.ClassId is { } classId && !await db.ClassLecturerCaches.AnyAsync(c => c.ClassId == classId, cancellationToken))
        {
            return Results.BadRequest(new { message = "Class not found or not yet synchronized; please try again or contact your administrator." });
        }

        var user = new User
        {
            Email = email,
            FullName = request.FullName,
            Role = request.Role,
            StudentCode = request.StudentCode,
            ClassId = request.ClassId,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(new UserRegistered(user.Id, user.Email, user.FullName, user.Role), cancellationToken);

        return Results.Created($"/auth/users/{user.Id}", new { user.Id, user.Email, Role = user.Role.ToString().ToLowerInvariant() });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IdentityDbContext db,
        IPasswordHasher<User> passwordHasher,
        JwtTokenGenerator tokenGenerator,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.PasswordHash is null)
        {
            return Results.Unauthorized();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Results.Unauthorized();
        }

        var token = tokenGenerator.GenerateToken(user.Id, user.Email, user.Role);

        return Results.Ok(new LoginResponse(token, user.Id, user.Email, user.Role.ToString().ToLowerInvariant()));
    }

    private static async Task<IResult> GoogleLoginAsync(
        GoogleLoginRequest request,
        IdentityDbContext db,
        IOptions<GoogleAuthOptions> googleOptions,
        JwtTokenGenerator tokenGenerator,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var options = googleOptions.Value;
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [options.ClientId],
            });
        }
        catch (InvalidJwtException)
        {
            return Results.Unauthorized();
        }

        var email = payload.Email.Trim().ToLowerInvariant();
        if (!payload.EmailVerified || !EducationEmailValidator.IsEducationEmail(email))
        {
            return Results.Forbid();
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubjectId == payload.Subject || u.Email == email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                FullName = payload.Name ?? email,
                Role = AppRole.Student,
                GoogleSubjectId = payload.Subject,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            await eventBus.PublishAsync(new UserRegistered(user.Id, user.Email, user.FullName, user.Role), cancellationToken);
        }
        else if (user.GoogleSubjectId is null)
        {
            user.GoogleSubjectId = payload.Subject;
            await db.SaveChangesAsync(cancellationToken);
        }

        var token = tokenGenerator.GenerateToken(user.Id, user.Email, user.Role);

        return Results.Ok(new LoginResponse(token, user.Id, user.Email, user.Role.ToString().ToLowerInvariant()));
    }
}

public sealed record RegisterRequest(string Email, string Password, string FullName, AppRole Role, string? StudentCode = null, Guid? ClassId = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record GoogleLoginRequest(string IdToken);

public sealed record LoginResponse(string Token, Guid UserId, string Email, string Role);
