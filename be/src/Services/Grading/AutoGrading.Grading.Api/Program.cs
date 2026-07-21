using AutoGrading.Common.Auth;
using AutoGrading.Common.Extensions;
using AutoGrading.Common.Jobs;
using AutoGrading.Common.Messaging;
using AutoGrading.Common.OpenCode;
using AutoGrading.Contracts.Events;
using AutoGrading.Grading.Api.Clients;
using AutoGrading.Grading.Api.Data;
using AutoGrading.Grading.Api.Endpoints;
using AutoGrading.Grading.Api.Handlers;
using AutoGrading.Grading.Api.Jobs;
using AutoGrading.Grading.Api.OpenCode;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<GradingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GradingDb")));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddJwtTokenGenerator(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

builder.Services.Configure<OpenCodeOptions>(builder.Configuration.GetSection(OpenCodeOptions.SectionName));
builder.Services.AddHttpClient<IOpenCodeClient, AutoGrading.Grading.Api.OpenCode.OpenCodeClient>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenCodeOptions>>().Value);

builder.Services.Configure<ServicesOptions>(builder.Configuration.GetSection(ServicesOptions.SectionName));
var servicesOptions = builder.Configuration.GetSection(ServicesOptions.SectionName).Get<ServicesOptions>() ?? new ServicesOptions();

builder.Services.AddTransient<ServiceAuthHandler>();
builder.Services.AddHttpClient<ICatalogApiClient, CatalogApiClient>(client =>
        client.BaseAddress = new Uri(servicesOptions.CatalogApiBaseUrl))
    .AddHttpMessageHandler<ServiceAuthHandler>();
builder.Services.AddHttpClient<ISubmissionApiClient, SubmissionApiClient>(client =>
        client.BaseAddress = new Uri(servicesOptions.SubmissionApiBaseUrl))
    .AddHttpMessageHandler<ServiceAuthHandler>();

builder.Services.AddScoped<AiGradingJob>();
builder.Services.AddScoped<ArtifactsExtractedHandler>();
builder.Services.AddScoped<RubricConfirmedHandler>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    // .UseInMemorySagaRepository(builder.Configuration.GetConnectionString("GradingDb")));
builder.Services.AddHangfireServer();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MigrateDatabase<GradingDbContext>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGradesEndpoints();
app.MapHealthChecks("/health");
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
});

var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<ArtifactsExtracted, ArtifactsExtractedHandler>();
eventBus.Subscribe<RubricConfirmed, RubricConfirmedHandler>();

app.Run();
