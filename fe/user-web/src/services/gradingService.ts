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

export async function getGradingRuns(submissionId: string): Promise<AiGradingRun[]> {
  try {
    const result = await apiGet<{ gradingRun?: AiGradingRun | null }>(`/grading/grades/${submissionId}/result`);
    return result.gradingRun ? [result.gradingRun] : [];
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return [];
    throw error;
  }
}

export async function getFinalGrade(submissionId: string): Promise<FinalGrade | null> {
  try {
    return await apiGet<FinalGrade>(`/grading/grades/${submissionId}/final`);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}
