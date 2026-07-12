# Phase 1: Shared AI Infrastructure

## Requirements

Move the existing `OpenRouterClient` from the Grading service into `AutoGrading.Common` where it can be shared by both Catalog (for rubric parsing) and Grading (for grading); extend it with a new rubric-criteria extraction method that parses AI responses into structured criteria objects following the existing defensive JSON-parsing pattern.

## Steps

1. Create the OpenRouter folder structure in `AutoGrading.Common` if it does not already exist.
2. Copy the current `OpenRouterClient.cs` from Grading to the new shared location, preserving all existing methods and the defensive JSON-parsing approach (using `JsonDocument.Parse` with `TryGetProperty`).
3. Add a new public method `ParseRubricCriteriaAsync` that accepts document text as input and returns a list of criteria objects (each with name, description, max score, and order fields).
4. Update the Grading service's `Program.cs` to import `OpenRouterClient` from the new `AutoGrading.Common` namespace instead of the local path.
5. Add `AutoGrading.Common` reference to Catalog's `Program.cs` DI and register the `OpenRouterClient` singleton alongside the existing `OpenRouterOptions` configuration binding.
6. Verify Grading service still compiles and its existing `AiGradingJob` can still call the client without changes.
7. Verify Catalog service can now reference and inject the shared `OpenRouterClient`.
8. Create a new xUnit test project `AutoGrading.Common.Tests` (`be/src/BuildingBlocks/AutoGrading.Common.Tests/`), referencing `AutoGrading.Common`, and add it to the solution. This is the first test project in the repo — keep the project minimal (just what's needed to run these tests).
9. Add `ParseRubricCriteriaAsyncTests` covering the defensive JSON-parsing path: a well-formed AI JSON response parses into the expected criteria list; a malformed/partial response (missing fields, non-JSON content) is handled gracefully (no unhandled exception) per the same defensive pattern as `TryParseResponse`.

## Success Criteria

- `OpenRouterClient` exists at `be/src/BuildingBlocks/AutoGrading.Common/OpenRouter/OpenRouterClient.cs`
- The class has the existing methods plus a new `ParseRubricCriteriaAsync(string documentText)` method
- Grading service compiles and tests pass (or run if existing tests exist)
- Catalog service can inject `OpenRouterClient` without errors
- The defensive JSON-parsing pattern (same as existing `TryParseResponse`) is used in the new rubric extraction method
- `AutoGrading.Common.Tests` project exists, is part of the solution, and `dotnet test` passes for `ParseRubricCriteriaAsyncTests` (both well-formed and malformed-response cases)

## Risks

- **Breaking existing Grading grading logic** — Moving the client could introduce namespace/import issues if any other code in Grading depends on the old location. *Mitigation:* Search the entire Grading service for any direct class references or path-based using statements before moving; update all imports.
- **Incomplete AI response parsing** — The rubric extraction response may contain fields not mapped correctly; the new method's defensive parsing must gracefully handle missing or malformed criteria. *Mitigation:* Implement the same `TryGetProperty` and null-coalescing pattern already used in `TryParseResponse`; log any extraction anomalies.

---
