using AutoGrading.Common.Auth;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Enums;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Data;
using AutoGrading.Identity.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AutoGrading.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);

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

        var user = new User
        {
            Email = email,
            FullName = request.FullName,
            Role = request.Role,
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

        if (user is null)
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
}

public sealed record RegisterRequest(string Email, string Password, string FullName, AppRole Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, Guid UserId, string Email, string Role);
