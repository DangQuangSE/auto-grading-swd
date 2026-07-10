import { finalCriterionScoreSchema } from "../lib/validation";
import { supabase } from "../lib/supabaseClient";

export type SaveFinalScoreInput = {
  submissionId: string;
  criterionId: string;
  aiCriterionScoreId?: string | null;
  finalScore: number;
  finalComment: string;
  maxScore: number;
  lecturerId: string;
};

export async function saveFinalCriterionScore(input: SaveFinalScoreInput) {
  finalCriterionScoreSchema.parse({
    criterionId: input.criterionId,
    aiCriterionScoreId: input.aiCriterionScoreId,
    finalScore: input.finalScore,
    finalComment: input.finalComment,
    maxScore: input.maxScore,
  });

  const { data, error } = await supabase
    .from("final_grades")
    .upsert(
      {
        submission_id: input.submissionId,
        rubric_criterion_id: input.criterionId,
        ai_criterion_score_id: input.aiCriterionScoreId ?? null,
        final_score: input.finalScore,
        final_comment: input.finalComment,
        reviewed_by: input.lecturerId,
        reviewed_at: new Date().toISOString(),
      },
      {
        onConflict: "submission_id,rubric_criterion_id",
      },
    )
    .select()
    .single();

  if (error) throw error;

  await supabase.from("submissions").update({ state: "reviewed" }).eq("id", input.submissionId);
  await supabase.from("audit_events").insert({
    actor_id: input.lecturerId,
    submission_id: input.submissionId,
    event_type: "lecturer_review_saved",
    details: {
      criterionId: input.criterionId,
      finalScore: input.finalScore,
    },
  });

  return data;
}

export async function publishSubmissionGrade(params: {
  submissionId: string;
  lecturerId: string;
}) {
  const { data: finalGrades, error: gradesError } = await supabase
    .from("final_grades")
    .select("final_score, rubric_criteria(max_score)")
    .eq("submission_id", params.submissionId);

  if (gradesError) throw gradesError;
  if (!finalGrades || finalGrades.length === 0) {
    throw new Error("Cannot publish without final criterion scores.");
  }

  const typedFinalGrades = finalGrades as unknown as Array<{
    final_score: number;
    rubric_criteria: { max_score?: number } | null;
  }>;

  const totals = typedFinalGrades.reduce(
    (acc, grade) => {
      const rubricCriteria = grade.rubric_criteria;
      return {
        totalScore: acc.totalScore + Number(grade.final_score),
        maxScore: acc.maxScore + Number(rubricCriteria?.max_score ?? 0),
      };
    },
    { totalScore: 0, maxScore: 0 },
  );

  const { data, error } = await supabase
    .from("grade_publications")
    .upsert(
      {
        submission_id: params.submissionId,
        published_by: params.lecturerId,
        total_score: totals.totalScore,
        max_score: totals.maxScore,
      },
      {
        onConflict: "submission_id",
      },
    )
    .select()
    .single();

  if (error) throw error;

  await supabase.from("submissions").update({ state: "published" }).eq("id", params.submissionId);
  await supabase.from("audit_events").insert({
    actor_id: params.lecturerId,
    submission_id: params.submissionId,
    event_type: "grade_published",
    details: totals,
  });

  return data;
}
