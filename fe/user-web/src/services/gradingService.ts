import { apiGet } from "../lib/apiClient";

export async function triggerExtraction(_submissionId: string, _actorId?: string) {
  return Promise.resolve(null);
}

export async function triggerAiGrading(_submissionId: string, _actorId?: string) {
  return Promise.resolve(null);
}

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
  const runs = await apiGet<AiGradingRun[]>(`/grading/grades/${submissionId}/runs`);
  return [...runs].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export async function getFinalGrade(submissionId: string): Promise<FinalGrade | null> {
  try {
    return await apiGet<FinalGrade>(`/grading/grades/${submissionId}/final`);
  } catch {
    return null;
  }
}
