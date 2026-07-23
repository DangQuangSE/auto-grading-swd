using AutoGrading.Catalog.Api.Endpoints;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Catalog.Api.Jobs;
using AutoGrading.Catalog.Api.Repository;
using AutoGrading.Common.Auth;
using AutoGrading.Common.Extensions;
using AutoGrading.Common.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb")));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddOpenCodeClient(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

builder.Services.AddScoped<RubricParsingJob>();
// EnrollmentQueries/EnrollmentCommands stay registered until Phase 3 switches Enrollment endpoints to IEnrollmentService.
builder.Services.AddScoped<EnrollmentQueries>();
builder.Services.AddScoped<EnrollmentCommands>();

builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IAssignmentRepository, AssignmentRepository>();
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<IRubricRepository, RubricRepository>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("CatalogDb")));
builder.Services.AddHangfireServer();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MigrateDatabase<CatalogDbContext>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapSubjectsEndpoints();
app.MapAssignmentsEndpoints();
app.MapRubricsEndpoints();
app.MapClassesEndpoints();
app.MapEnrollmentsEndpoints();
app.MapHealthChecks("/health");
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
});

app.Run();
