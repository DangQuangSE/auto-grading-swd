using AutoGrading.Identity.Api.Interfaces;
using AutoGrading.Identity.Api.Repository;
using AutoGrading.Identity.Api.Service;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGrading.Identity.Api.Extensions;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IUserRepository"/>, the EF Core-backed data access layer.</summary>
    public static IServiceCollection AddIdentityRepository(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }

    /// <summary>Registers <see cref="IAuthService"/>/<see cref="IUserService"/>, the business-logic layer
    /// consumed by endpoints and event handlers.</summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
