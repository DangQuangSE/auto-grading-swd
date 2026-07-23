namespace AutoGrading.Catalog.Api.Endpoints;

public sealed record UpsertEnrollmentRequest(Guid ClassId, string? RowVersion);
