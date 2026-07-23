using AutoGrading.SubmissionSvc.Api.Interfaces;
using AutoGrading.SubmissionSvc.Api.Repository;
using AutoGrading.SubmissionSvc.Api.Service;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGrading.SubmissionSvc.Api.Extensions;

public static class SubmissionServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISubmissionRepository"/>, the EF Core-backed data access layer.</summary>
    public static IServiceCollection AddSubmissionRepository(this IServiceCollection services)
    {
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();

        return services;
    }

    /// <summary>Registers <see cref="ISubmissionService"/>, the business-logic layer consumed by endpoints and jobs.</summary>
    public static IServiceCollection AddSubmissionApplication(this IServiceCollection services)
    {
        services.AddScoped<ISubmissionService, SubmissionService>();

        return services;
    }
}
