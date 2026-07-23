using AutoGrading.Grading.Api.Interfaces;
using AutoGrading.Grading.Api.Repository;
using AutoGrading.Grading.Api.Service;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGrading.Grading.Api.Extensions;

public static class GradingServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IGradingRepository"/>, the EF Core-backed data access layer.</summary>
    public static IServiceCollection AddGradingRepository(this IServiceCollection services)
    {
        services.AddScoped<IGradingRepository, GradingRepository>();

        return services;
    }

    /// <summary>Registers <see cref="IGradingService"/>, the business-logic layer consumed by endpoints and jobs.</summary>
    public static IServiceCollection AddGradingApplication(this IServiceCollection services)
    {
        services.AddScoped<IGradingService, GradingService>();

        return services;
    }
}
