using AutoGrading.Contracts.Enums;

namespace AutoGrading.Identity.Api.Dto;

public sealed record RegisterRequest(string Email, string Password, string FullName, AppRole Role, string? StudentCode = null, Guid? ClassId = null);
