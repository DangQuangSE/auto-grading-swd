using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoGrading.Common.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>Applies pending EF Core migrations for <typeparamref name="TContext"/> on startup.</summary>
    public static void MigrateDatabase<TContext>(this WebApplication app) where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreated();
    }
}
