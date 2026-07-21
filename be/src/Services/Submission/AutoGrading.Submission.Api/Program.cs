using AutoGrading.Common.Auth;
using AutoGrading.Common.Extensions;
using AutoGrading.Common.Jobs;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.SubmissionSvc.Api.Data;
using AutoGrading.SubmissionSvc.Api.Endpoints;
using AutoGrading.SubmissionSvc.Api.Jobs;
using AutoGrading.SubmissionSvc.Api.Parsing;
using Hangfire;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<SubmissionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SubmissionDb")));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);
builder.Services.AddObjectStorage(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

builder.Services.AddScoped<DocxReportParser>();
builder.Services.AddScoped<DrawioDiagramParser>();
builder.Services.AddScoped<IArtifactParser, ArtifactParser>();
builder.Services.AddScoped<ExtractionJob>();
builder.Services.AddScoped<SubmissionUploadedHandler>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    );
    // .UseInMemorySagaRepository(builder.Configuration.GetConnectionString("SubmissionDb")));
builder.Services.AddHangfireServer();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MigrateDatabase<SubmissionDbContext>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapSubmissionsEndpoints();
app.MapHealthChecks("/health");
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
});

var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<SubmissionUploaded, SubmissionUploadedHandler>();

app.Run();
