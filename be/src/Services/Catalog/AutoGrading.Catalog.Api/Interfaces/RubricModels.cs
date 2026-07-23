using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Interfaces;

/// <summary>Rubric-upload metadata handed to <see cref="IRubricService.UploadAsync"/> — deliberately excludes
/// <c>IFormFile</c>; the endpoint uploads the file to object storage first and passes the resulting <see cref="ObjectKey"/>
/// down, keeping the Service layer free of storage-infrastructure and HTTP-binding types.</summary>
public sealed record RubricUploadRequest(
    Guid SubjectId,
    Guid? AssignmentId,
    string Name,
    RubricScope Scope,
    string ObjectKey);

/// <summary>Returned by <see cref="IRubricService.UploadAsync"/>. <see cref="PreviousObjectKeyToDelete"/> is set when the
/// upload replaced an existing rubric's file — the endpoint deletes that key from object storage, since Service does not
/// touch <c>IObjectStorage</c> for uploads.</summary>
public sealed record RubricUploadResult(Rubric Rubric, string? PreviousObjectKeyToDelete);

/// <summary>One criterion row from <c>PATCH /rubrics/{id}/criteria</c>, translated to this Service-layer shape by the
/// endpoint so <see cref="IRubricService.UpdateCriteriaAsync"/> never depends on an Endpoints-namespace request record.</summary>
public sealed record RubricCriterionInput(string Name, string? Description, decimal MaxScore, int OrderIndex);
