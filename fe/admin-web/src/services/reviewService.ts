import { finalCriterionScoreSchema } from "../lib/validation";
import { apiGet, apiPost } from "../lib/apiClient";

export async function triggerRegrade(params: { submissionId: string; assignmentDescription?: string | null }) {
  return apiPost(`/grading/grades/${params.submissionId}/regrade`, {
    assignmentDescription: params.assignmentDescription ?? null,
  });
}

export type SaveFinalScoreInput = {
  submissionId: string;
  criterionId: string;
  aiCriterionScoreId?: string | null;
  finalScore: number;
  finalComment: string;
  maxScore: number;
  lecturerId: string;
};

// The backend's FinalGrade is one score per submission, not per criterion, so
// there is no endpoint to persist a per-criterion override yet. This keeps the
// edited value in the caller's local state only (see SubmissionReviewPage's
// finalScores state) until that endpoint exists.
export async function saveFinalCriterionScore(input: SaveFinalScoreInput) {
  finalCriterionScoreSchema.parse({
    criterionId: input.criterionId,
    aiCriterionScoreId: input.aiCriterionScoreId,
    finalScore: input.finalScore,
    finalComment: input.finalComment,
    maxScore: input.maxScore,
  });

  return Promise.resolve(input);
}

type GradingRunSummary = {
  id: string;
  createdAt: string;
  scores: { suggestedScore: number; maxScore: number }[];
};

export async function publishSubmissionGrade(params: {
  submissionId: string;
  lecturerId: string;
}) {
  const runs = await apiGet<GradingRunSummary[]>(`/grading/grades/${params.submissionId}/runs`);
  const latestRun = [...runs].sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];

  if (!latestRun || latestRun.scores.length === 0) {
    throw new Error("Cannot publish without AI criterion scores.");
  }

  const totals = latestRun.scores.reduce(
    (acc, score) => ({
      totalScore: acc.totalScore + Number(score.suggestedScore),
      maxScore: acc.maxScore + Number(score.maxScore),
    }),
    { totalScore: 0, maxScore: 0 },
  );

  return apiPost(`/grading/grades/${params.submissionId}/publish`, {
    gradingRunId: latestRun.id,
    finalScore: totals.totalScore,
    notes: null,
  });
}
