using AutoGrading.Contracts.Enums;

namespace AutoGrading.Identity.Api.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public AppRole Role { get; set; } = AppRole.Student;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
