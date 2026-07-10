using AutoGrading.Contracts.Enums;

namespace AutoGrading.Contracts.Events;

/// <summary>Published by Identity Service after a new user account is created.</summary>
public sealed record UserRegistered(Guid UserId, string Email, string FullName, AppRole Role) : IntegrationEvent;
