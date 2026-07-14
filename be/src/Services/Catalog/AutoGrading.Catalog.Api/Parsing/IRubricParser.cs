namespace AutoGrading.Catalog.Api.Parsing;

public interface IRubricParser
{
    RubricParseResult Parse(Stream fileStream);
}

public sealed record RubricParseResult(bool Success, IReadOnlyList<ParsedRubricCriterion> Criteria, IReadOnlyList<string> Errors);

public sealed record ParsedRubricCriterion(
    string CriterionCode,
    string Title,
    string? Description,
    decimal MaxScore,
    string? GradingGuidance,
    string? DeductionNotes,
    int DisplayOrder);
