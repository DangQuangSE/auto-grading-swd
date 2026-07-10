import { getSubmissionReviewData } from "../services/gradingService";
import { publishSubmissionGrade, saveFinalCriterionScore, type SaveFinalScoreInput } from "../services/reviewService";

export async function loadReviewWorkflow(submissionId: string) {
  return getSubmissionReviewData(submissionId);
}

export async function saveReviewWorkflow(params: {
  lecturerId: string;
  scores: SaveFinalScoreInput[];
}) {
  const saved = [];

  for (const score of params.scores) {
    saved.push(
      await saveFinalCriterionScore({
        ...score,
        lecturerId: params.lecturerId,
      }),
    );
  }

  return saved;
}

export async function publishReviewWorkflow(params: {
  submissionId: string;
  lecturerId: string;
}) {
  return publishSubmissionGrade(params);
}
