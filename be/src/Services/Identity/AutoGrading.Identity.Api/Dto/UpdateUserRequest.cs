namespace AutoGrading.Identity.Api.Dto;

public sealed record UpdateUserRequest(string? StudentCode, Guid? ClassId);
