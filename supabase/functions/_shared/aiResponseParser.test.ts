import { describe, expect, it } from "vitest";
import { parseAiGradingResponse } from "./aiResponseParser";

const criterionId = "00000000-0000-4000-8000-000000000001";

describe("parseAiGradingResponse", () => {
  it("parses structured criterion scores", () => {
    const parsed = parseAiGradingResponse(
      JSON.stringify({
        criterion_scores: [
          {
            criterion_id: criterionId,
            max_score: 5,
            suggested_score: 4,
            deductions: [{ reason: "Missing retry flow", points: 1 }],
            evidence: [{ source: "document", reference: "Section 2.3" }],
            comment: "Mostly complete.",
            confidence: "high",
          },
        ],
        overall_comment: "Good submission.",
      }),
      [{ id: criterionId, max_score: 5 }],
    );

    expect(parsed.criterionScores[0].suggestedScore).toBe(4);
    expect(parsed.overallComment).toBe("Good submission.");
  });

  it("rejects scores above rubric max score", () => {
    expect(() =>
      parseAiGradingResponse(
        JSON.stringify({
          criterion_scores: [{ criterion_id: criterionId, max_score: 5, suggested_score: 6 }],
        }),
        [{ id: criterionId, max_score: 5 }],
      ),
    ).toThrow(/exceeds max score/i);
  });
});
