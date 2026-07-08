import { supabase } from "../lib/supabaseClient";

export async function triggerExtraction(submissionId: string, actorId?: string) {
  const { data, error } = await supabase.functions.invoke("extract-submission", {
    body: { submissionId, actorId },
  });

  if (error) throw error;
  return data;
}

export async function triggerAiGrading(submissionId: string, actorId?: string) {
  const { data, error } = await supabase.functions.invoke("grade-submission", {
    body: { submissionId, actorId },
  });

  if (error) throw error;
  return data;
}

export async function getSubmissionReviewData(submissionId: string) {
  const [submission, artifacts, scores] = await Promise.all([
    supabase.from("submissions").select("*").eq("id", submissionId).single(),
    supabase.from("extracted_artifacts").select("*").eq("submission_id", submissionId),
    supabase
      .from("ai_criterion_scores")
      .select("*, rubric_criteria(*)")
      .eq("submission_id", submissionId)
      .order("created_at", { ascending: false }),
  ]);

  if (submission.error) throw submission.error;
  if (artifacts.error) throw artifacts.error;
  if (scores.error) throw scores.error;

  return {
    submission: submission.data,
    artifacts: artifacts.data,
    aiScores: scores.data,
  };
}

export async function retrySubmission(submissionId: string, actorId?: string) {
  await triggerExtraction(submissionId, actorId);
  return triggerAiGrading(submissionId, actorId);
}
