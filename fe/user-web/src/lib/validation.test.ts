import { describe, expect, it } from "vitest";
import { aiCriterionScoreSchema, finalCriterionScoreSchema } from "./validation";

describe("score validation", () => {
  it("rejects AI suggested scores above the criterion max score", () => {
    expect(() =>
      aiCriterionScoreSchema.parse({
        criterionId: "00000000-0000-4000-8000-000000000000",
        maxScore: 5,
        suggestedScore: 6,
      }),
    ).toThrow(/cannot exceed max score/i);
  });

  it("rejects final lecturer scores above the criterion max score", () => {
    expect(() =>
      finalCriterionScoreSchema.parse({
        criterionId: "00000000-0000-4000-8000-000000000000",
        maxScore: 10,
        finalScore: 10.5,
      }),
    ).toThrow(/cannot exceed max score/i);
  });
});
