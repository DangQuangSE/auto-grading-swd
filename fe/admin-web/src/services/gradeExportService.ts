import { apiGet } from "../lib/apiClient";
import { MAX_PAGE_SIZE, type PagedResult } from "../lib/pagination";
import type { Assignment } from "./subjectService";

export type FinalGrade = {
  submissionId: string;
  finalGradeId: string;
  finalScore: number;
  createdAt: string;
};

export async function getAssignments() {
  const result = await apiGet<PagedResult<Assignment>>(`/catalog/assignments?page=1&pageSize=${MAX_PAGE_SIZE}`);
  return result.items;
}

export async function batchGetGrades(submissionIds: string[]) {
  const dedupedIds = [...new Set(submissionIds)];
  if (dedupedIds.length === 0) {
    return [];
  }

  return apiGet<FinalGrade[]>(`/grading/grades/final?submissionIds=${dedupedIds.join(",")}`);
}
