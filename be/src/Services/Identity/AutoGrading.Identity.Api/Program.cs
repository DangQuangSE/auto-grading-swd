using AutoGrading.Common.Auth;
using AutoGrading.Common.Extensions;
using AutoGrading.Common.Messaging;
using AutoGrading.Contracts.Enums;
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

if (app.Configuration.GetValue<bool>("Seed:TestAccounts"))
{
    await SeedTestAccountsAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapHealthChecks("/health");

var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<ClassLecturerAssigned, ClassLecturerAssignedHandler>();
eventBus.Subscribe<SubmissionUploaded, SubmissionUploadedHandler>();
eventBus.Subscribe<GradePublished, GradePublishedHandler>();

app.Run();

/// <summary>Seeds fixed dev/test accounts (one per role) for local docker-compose testing.
/// Gated behind Seed:TestAccounts config (only set in docker-compose.yml's identity-api service) —
/// never runs unless explicitly enabled, and is idempotent (skips accounts that already exist).</summary>
static async Task SeedTestAccountsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

    (string Email, string FullName, AppRole Role)[] testAccounts =
    [
        ("teststudent1@fpt.edu.vn", "Test Student 1", AppRole.Student),
        ("teststudent2@fpt.edu.vn", "Test Student 2", AppRole.Student),
        ("teststudent3@fpt.edu.vn", "Test Student 3", AppRole.Student),
        ("teststudent4@fpt.edu.vn", "Test Student 4", AppRole.Student),
        ("teststudent5@fpt.edu.vn", "Test Student 5", AppRole.Student),
        ("teststudent6@fpt.edu.vn", "Test Student 6", AppRole.Student),
        ("teststudent7@fpt.edu.vn", "Test Student 7", AppRole.Student),
        ("teststudent8@fpt.edu.vn", "Test Student 8", AppRole.Student),
        ("teststudent9@fpt.edu.vn", "Test Student 9", AppRole.Student),
        ("teststudent10@fpt.edu.vn", "Test Student 10", AppRole.Student),
        ("testlecturer1@fpt.edu.vn", "Test Lecturer 1", AppRole.Lecturer),
        ("testlecturer2@fpt.edu.vn", "Test Lecturer 2", AppRole.Lecturer),
        ("testlecturer3@fpt.edu.vn", "Test Lecturer 3", AppRole.Lecturer),
        ("testlecturer4@fpt.edu.vn", "Test Lecturer 4", AppRole.Lecturer),
        ("testadmin1@fpt.edu.vn", "Test Admin", AppRole.Admin),
    ];

    foreach (var (email, fullName, role) in testAccounts)
    {
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            continue;
        }

        var user = new User { Email = email, FullName = fullName, Role = role };
        user.PasswordHash = passwordHasher.HashPassword(user, "Test@12345");
        db.Users.Add(user);
    }

    await db.SaveChangesAsync();
}
