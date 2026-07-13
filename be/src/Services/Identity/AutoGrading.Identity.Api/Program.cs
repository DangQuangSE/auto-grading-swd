using AutoGrading.Common.Auth;
using AutoGrading.Common.Extensions;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Events;
using AutoGrading.Identity.Api.Auth;
using AutoGrading.Identity.Api.Data;
using AutoGrading.Identity.Api.Domain;
using AutoGrading.Identity.Api.Endpoints;
using AutoGrading.Identity.Api.Handlers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb")));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddJwtTokenGenerator(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddEventBus(builder.Configuration);
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));

builder.Services.AddScoped<ClassLecturerAssignedHandler>();
builder.Services.AddScoped<SubmissionUploadedHandler>();
builder.Services.AddScoped<GradePublishedHandler>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MigrateDatabase<IdentityDbContext>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapHealthChecks("/health");

var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<ClassLecturerAssigned, ClassLecturerAssignedHandler>();
eventBus.Subscribe<SubmissionUploaded, SubmissionUploadedHandler>();
eventBus.Subscribe<GradePublished, GradePublishedHandler>();

app.Run();
