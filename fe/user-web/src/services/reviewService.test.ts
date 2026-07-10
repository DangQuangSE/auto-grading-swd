import { describe, expect, it } from "vitest";
import { saveFinalCriterionScore } from "./reviewService";

describe("saveFinalCriterionScore", () => {
  it("returns the validated input for local state (no per-criterion persistence yet)", async () => {
    const input = {
      submissionId: "submission-id",
      criterionId: "00000000-0000-4000-8000-000000000001",
      aiCriterionScoreId: "00000000-0000-4000-8000-000000000002",
      finalScore: 4,
      finalComment: "Approved with minor edit.",
      maxScore: 5,
      lecturerId: "lecturer-id",
    };

    const result = await saveFinalCriterionScore(input);

    expect(result).toEqual(input);
  });

  it("rejects a final score above the criterion's max score", async () => {
    await expect(
      saveFinalCriterionScore({
        submissionId: "submission-id",
        criterionId: "00000000-0000-4000-8000-000000000001",
        finalScore: 10,
        finalComment: "Out of range.",
        maxScore: 5,
        lecturerId: "lecturer-id",
      }),
    ).rejects.toThrow();
  });
});
