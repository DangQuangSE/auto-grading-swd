using System.Security.Claims;
using AutoGrading.Catalog.Api.Domain;
using AutoGrading.Catalog.Api.Dto;
using AutoGrading.Catalog.Api.Interfaces;
using AutoGrading.Catalog.Api.Jobs;
using AutoGrading.Common.Auth;
using AutoGrading.Common.Messaging;
using AutoGrading.Common.Storage;
using AutoGrading.Contracts.Events;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace AutoGrading.Catalog.Api.Endpoints;

public static class RubricsEndpoints
{
    public static IEndpointRouteBuilder MapRubricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rubrics").WithTags("Rubrics");

        group.MapGet("/", async (Guid? subjectId, Guid? assignmentId, ClaimsPrincipal user, IRubricService service, CancellationToken ct) =>
            {
                var rubrics = await service.ListAsync(subjectId, assignmentId, user.GetUserId(), user.IsInRole("admin"), ct);
                return Results.Ok(rubrics.Select(RubricResponse.FromDomain).ToList());
            })
            .RequireAuthorization();

        group.MapGet("/{id:guid}/file", async (Guid id, ClaimsPrincipal user, IRubricService service, IObjectStorage storage, CancellationToken ct) =>
            {
                Rubric? rubric;
                try
                {
                    rubric = await service.DownloadFileAsync(id, user.GetUserId(), user.IsInRole("admin"), ct);
                }
                catch (RubricForbiddenException)
                {
                    return Results.Forbid();
                }

                if (rubric?.FileObjectKey is null)
                {
                    return Results.NotFound();
                }

                var stream = await storage.DownloadAsync(rubric.FileObjectKey, ct);
                return Results.File(
                    stream,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    rubric.Name);
            })
            .RequireAuthorization();

        group.MapPost("/upload", UploadRubricAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"))
            .DisableAntiforgery();

        group.MapPost("/{id:guid}/retry-parsing", RetryParsingAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPatch("/{id:guid}/criteria", UpdateCriteriaAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/{id:guid}/confirm", ConfirmRubricAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        group.MapPost("/{id:guid}/unlock", UnlockRubricAsync)
            .RequireAuthorization(policy => policy.RequireRole("lecturer", "admin"));

        return app;
    }

    private static async Task<IResult> UploadRubricAsync(
        [FromForm] UploadRubricForm form,
        ClaimsPrincipal user,
        IRubricService service,
        IObjectStorage storage,
        IBackgroundJobClient backgroundJobs,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var isAdmin = user.IsInRole("admin");

        Guid? existingRubricId;
        try
        {
            existingRubricId = await service.AuthorizeUploadAsync(form.AssignmentId, form.Scope, userId, isAdmin, cancellationToken);
        }
        catch (RubricForbiddenException)
        {
            return Results.Forbid();
        }

        var objectKey = $"rubrics/{Guid.NewGuid()}-{form.File.FileName}";
        await using (var stream = form.File.OpenReadStream())
        {
            await storage.UploadAsync(objectKey, stream, form.File.ContentType, cancellationToken);
        }

        var result = await service.UploadAsync(
            existingRubricId,
            new RubricUploadRequest(form.SubjectId, form.AssignmentId, form.Name, form.Scope, objectKey),
            userId,
            cancellationToken);

        if (!string.IsNullOrEmpty(result.PreviousObjectKeyToDelete))
        {
            await storage.DeleteAsync(result.PreviousObjectKeyToDelete, cancellationToken);
        }

        backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(result.Rubric.Id, CancellationToken.None));

        return Results.Created($"/rubrics/{result.Rubric.Id}", RubricResponse.FromDomain(result.Rubric));
    }

    private static async Task<IResult> RetryParsingAsync(
        Guid id,
        ClaimsPrincipal user,
        IRubricService service,
        IBackgroundJobClient backgroundJobs,
        CancellationToken cancellationToken)
    {
        Rubric rubric;
        try
        {
            rubric = await service.RetryParsingAsync(id, user.GetUserId(), user.IsInRole("admin"), cancellationToken);
        }
        catch (CatalogNotFoundException)
        {
            return Results.NotFound();
        }
        catch (RubricForbiddenException)
        {
            return Results.Forbid();
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }

        backgroundJobs.Enqueue<RubricParsingJob>(job => job.ExecuteAsync(rubric.Id, CancellationToken.None));

        return Results.Accepted();
    }

    private static async Task<IResult> UpdateCriteriaAsync(
        Guid id,
        List<UpdateCriterionRequest> request,
        ClaimsPrincipal user,
        IRubricService service,
        CancellationToken cancellationToken)
    {
        var criteria = request
            .Select(criterion => new RubricCriterionInput(criterion.Name, criterion.Description, criterion.MaxScore, criterion.OrderIndex))
            .ToList();

        try
        {
            var newCriteria = await service.UpdateCriteriaAsync(id, criteria, user.GetUserId(), user.IsInRole("admin"), cancellationToken);
            return Results.Ok(newCriteria);
        }
        catch (CatalogNotFoundException)
        {
            return Results.NotFound();
        }
        catch (RubricForbiddenException)
        {
            return Results.Forbid();
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }

    private static async Task<IResult> ConfirmRubricAsync(
        Guid id,
        ClaimsPrincipal user,
        IRubricService service,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        Rubric rubric;
        try
        {
            rubric = await service.ConfirmAsync(id, user.GetUserId(), user.IsInRole("admin"), cancellationToken);
        }
        catch (CatalogNotFoundException)
        {
            return Results.NotFound();
        }
        catch (RubricForbiddenException)
        {
            return Results.Forbid();
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }

        await eventBus.PublishAsync(
            new RubricConfirmed(
                rubric.Id,
                rubric.SubjectId,
                rubric.AssignmentId,
                rubric.Scope.ToString(),
                rubric.Criteria
                    .Select(c => new RubricConfirmedCriterion(c.Id, c.Name, c.Description, c.MaxScore, c.OrderIndex))
                    .ToList()),
            cancellationToken);

        return Results.Ok(RubricResponse.FromDomain(rubric));
    }

    private static async Task<IResult> UnlockRubricAsync(
        Guid id,
        ClaimsPrincipal user,
        IRubricService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var rubric = await service.UnlockAsync(id, user.GetUserId(), user.IsInRole("admin"), cancellationToken);
            return Results.Ok(RubricResponse.FromDomain(rubric));
        }
        catch (CatalogNotFoundException)
        {
            return Results.NotFound();
        }
        catch (RubricForbiddenException)
        {
            return Results.Forbid();
        }
        catch (CatalogConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }
}

public sealed class UploadRubricForm
{
    public Guid SubjectId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
    public RubricScope Scope { get; set; } = RubricScope.Lecturer;
}
