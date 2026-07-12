using AutoGrading.Common.Auth;
using AutoGrading.Common.Messaging;
using AutoGrading.Common.OpenRouter;
using AutoGrading.Common.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGrading.Common.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the RabbitMQ-backed <see cref="IEventBus"/> as a singleton, bound to the "RabbitMq" config section.</summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IEventBus, RabbitMqEventBus>();

        return services;
    }

    /// <summary>Registers the MinIO-backed <see cref="IObjectStorage"/>, bound to the "Minio" config section.</summary>
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.AddSingleton<IObjectStorage, MinioStorage>();

        return services;
    }

    /// <summary>Registers the JWT token generator used by the Identity Service to issue tokens.</summary>
    public static IServiceCollection AddJwtTokenGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenGenerator>();

        return services;
    }

    /// <summary>Registers the shared OpenRouter AI client, bound to the "OpenRouter" config section.</summary>
    public static IServiceCollection AddOpenRouterClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenRouterOptions>(configuration.GetSection(OpenRouterOptions.SectionName));
        services.AddHttpClient<IOpenRouterClient, OpenRouterClient>();

        return services;
    }
}
