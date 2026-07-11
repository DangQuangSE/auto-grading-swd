import { apiGet } from "../lib/apiClient";
import type { SubmissionRecord } from "./submissionService";

type RawArtifact = {
  id: string;
  submissionId: string;
  kind: "report" | "diagram";
  content?: string | null;
  warnings?: string | null;
  createdAt: string;
};

type RawSubmissionDetail = SubmissionRecord & { artifacts?: RawArtifact[] };

type RawCriterionScore = {
  id: string;
  gradingRunId: string;
  submissionId: string;
  rubricCriterionId: string;
  maxScore: number;
  suggestedScore: number;
  deductions?: string | null;
  evidence?: string | null;
  comment?: string | null;
  confidence?: number | null;
};

type RawGradingRun = {
  id: string;
  submissionId: string;
  model: string;
  status: string;
  createdAt: string;
  completedAt?: string | null;
  scores: RawCriterionScore[];
};

// The backend runs extraction and AI grading automatically as background jobs
// (Hangfire handlers reacting to SubmissionUploaded/ArtifactsExtracted events),
// so there is no manual trigger endpoint. These are kept as no-ops so callers
// that used to kick off Supabase Edge Functions keep working unchanged.
export async function triggerExtraction(_submissionId: string, _actorId?: string) {
  return Promise.resolve(null);
}

export async function triggerAiGrading(_submissionId: string, _actorId?: string) {
  return Promise.resolve(null);
}

export async function listRecentSubmissions() {
  const submissions = await apiGet<SubmissionRecord[]>("/submissions/submissions");
  return [...submissions]
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .slice(0, 20)
    .map((submission) => ({
      id: submission.id,
      assignment_id: submission.assignmentId,
      student_id: submission.studentId,
      state: submission.state,
      submitted_at: submission.createdAt,
    }));
}

function toArtifactType(kind: RawArtifact["kind"]) {
  return kind === "report" ? "document" : "diagram";
}

export async function getSubmissionReviewData(submissionId: string) {
  const [submission, runs] = await Promise.all([
    apiGet<RawSubmissionDetail>(`/submissions/submissions/${submissionId}`),
    apiGet<RawGradingRun[]>(`/grading/grades/${submissionId}/runs`),
  ]);

  const latestRun = [...runs].sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];

  const aiScores = (latestRun?.scores ?? []).map((score) => ({
    id: score.id,
    rubric_criterion_id: score.rubricCriterionId,
    suggested_score: score.suggestedScore,
    max_score: score.maxScore,
    comment: score.comment ?? null,
    evidence: score.evidence ?? null,
  }));

  const artifacts = (submission.artifacts ?? []).map((artifact) => ({
    id: artifact.id,
    artifact_type: toArtifactType(artifact.kind),
    content: artifact.content ?? null,
    warnings: artifact.warnings ?? null,
  }));

  return {
    submission,
    artifacts,
    aiScores,
  };
}

export async function retrySubmission(submissionId: string, actorId?: string) {
  await triggerExtraction(submissionId, actorId);
  return triggerAiGrading(submissionId, actorId);
}
