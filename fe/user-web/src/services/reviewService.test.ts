import { describe, expect, it, vi } from "vitest";

const upsert = vi.fn();
const insert = vi.fn();
const update = vi.fn();

vi.mock("../lib/supabaseClient", () => ({
  supabase: {
    from: (table: string) => {
      if (table === "final_grades") {
        return {
          upsert,
          select: vi.fn(),
        };
      }

      if (table === "submissions") {
        return {
          update: vi.fn(() => ({ eq: update })),
        };
      }

      if (table === "audit_events") {
        return { insert };
      }

      return {};
    },
  },
}));

describe("saveFinalCriterionScore", () => {
  it("stores final lecturer scores separately from AI suggestions", async () => {
    upsert.mockReturnValue({
      select: () => ({
        single: () => ({
          data: { id: "final-grade-id" },
          error: null,
        }),
      }),
    });
    update.mockResolvedValue({ error: null });
    insert.mockResolvedValue({ error: null });

    const { saveFinalCriterionScore } = await import("./reviewService");
    const result = await saveFinalCriterionScore({
      submissionId: "submission-id",
      criterionId: "00000000-0000-4000-8000-000000000001",
      aiCriterionScoreId: "00000000-0000-4000-8000-000000000002",
      finalScore: 4,
      finalComment: "Approved with minor edit.",
      maxScore: 5,
      lecturerId: "lecturer-id",
    });

    expect(result).toEqual({ id: "final-grade-id" });
    expect(upsert).toHaveBeenCalledWith(
      expect.objectContaining({
        final_score: 4,
        ai_criterion_score_id: "00000000-0000-4000-8000-000000000002",
      }),
      { onConflict: "submission_id,rubric_criterion_id" },
    );
  });
});
