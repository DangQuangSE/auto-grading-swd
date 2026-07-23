import { ApiError, apiGet } from "../lib/apiClient";

export type AiCriterionScore = {
  id: string;
  rubricCriterionId: string;
  maxScore: number;
  suggestedScore: number;
  deductions?: string | null;
  evidence?: string | null;
  comment?: string | null;
  confidence?: number | null;
};

export type AiGradingRun = {
  id: string;
  submissionId: string;
  model: string;
  status: "running" | "completed" | "failed";
  createdAt: string;
  completedAt?: string | null;
  scores: AiCriterionScore[];
};

export type FinalGrade = {
  id: string;
  submissionId: string;
  finalScore: number;
  notes?: string | null;
  createdAt: string;
};

export type GradingResult = {
  gradingRun: AiGradingRun | null;
  isPublished: boolean;
  /** Grading is complete on the backend but lecturer has not published yet. */
  gradingDone: boolean;
};

export async function getGradingResult(submissionId: string): Promise<GradingResult> {
  try {
    const result = await apiGet<{ finalGrade?: FinalGrade | null; gradingRun?: AiGradingRun | null }>(
      `/grading/grades/${submissionId}/result`
    );
    return {
      gradingRun: result.gradingRun ?? null,
      isPublished: true,
      gradingDone: true,
    };
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      // Backend returns { gradingDone: bool } in the 404 body when grading is complete
      // but the grade has not been published yet.
      const body = error.body as { gradingDone?: boolean } | null;
      const gradingDone = body?.gradingDone === true;
      return { gradingRun: null, isPublished: false, gradingDone };
    }
    throw error;
  }
}

/** @deprecated Use getGradingResult instead. Kept as shim for any legacy callers. */
export async function getGradingRuns(submissionId: string): Promise<AiGradingRun[]> {
  const result = await getGradingResult(submissionId);
  return result.gradingRun ? [result.gradingRun] : [];
}

export async function getFinalGrade(submissionId: string): Promise<FinalGrade | null> {
  try {
    return await apiGet<FinalGrade>(`/grading/grades/${submissionId}/final`);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}
