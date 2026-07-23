namespace AutoGrading.Identity.Api.Interfaces;

public sealed record UserSummaryData(Guid Id, string Email, string FullName, string Role, string? StudentCode, Guid? ClassId, string? ClassName);

public sealed record RosterImportRowOutcome(int RowNumber, string Email, string Status, string? Reason);

public sealed record RosterImportResult(int TotalRows, int UpdatedCount, int SkippedCount, IReadOnlyList<RosterImportRowOutcome> Details);

/// <summary>Formerly the return type of the static <c>RosterAuthorization.AuthorizeAsync</c> helper —
/// that class is retired as of Phase 3; its logic and this enum now live with <c>UserService</c>,
/// the only consumer.</summary>
public enum RosterAuthorizationResult
{
    Admin,
    ClassLecturer,
    Grader,
    Denied,
}

public interface IUserService
{
    Task<List<UserSummaryData>> ListAsync(IReadOnlyCollection<Guid>? ids, RequesterContext requester, CancellationToken ct);

    Task<UserSummaryData> UpdateAsync(Guid userId, string? studentCode, Guid? classId, RequesterContext requester, CancellationToken ct);

    Task<RosterImportResult> BulkImportAsync(Stream fileStream, string fileName, RequesterContext requester, CancellationToken ct);
}

/// <summary>Thrown when a caller (typically a lecturer) is not authorized to read/modify a specific
/// student's roster fields — distinct from the route-level role policy, which is a coarser first gate.</summary>
public sealed class RosterAuthorizationException() : Exception("Not authorized to modify this student's roster fields.");

/// <summary>Thrown by <c>UserService.BulkImportAsync</c> when <c>RosterFileParser.Parse</c> reports a
/// parse-level error (e.g. missing required column) — replaces the endpoint's <c>400 BadRequest</c>.</summary>
public sealed class RosterFileParseException(string message) : Exception(message);
