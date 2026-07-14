import { assertValidFileExtension } from "../lib/validation";
import { apiGet, apiGetBlob, apiPatch, apiPost, apiPostForm } from "../lib/apiClient";

export type RubricStatus = "parsing" | "draft" | "confirmed";
export type RubricScope = "lecturer" | "schoolWide";

export type RubricCriterion = {
  id: string;
  rubricId: string;
  code: string;
  name: string;
  description?: string | null;
  maxScore: number;
  orderIndex: number;
};

export type RubricListItem = {
  id: string;
  subjectId: string;
  assignmentId?: string | null;
  name: string;
  fileObjectKey?: string | null;
  createdAt: string;
  status: RubricStatus;
  scope: RubricScope;
  lecturerId?: string | null;
  criteria: RubricCriterion[];
};

export type RubricCriterionInput = {
  name: string;
  description?: string | null;
  maxScore: number;
  orderIndex: number;
};

export async function uploadRubricDocx(params: {
  subjectId: string;
  assignmentId?: string | null;
  file: File;
  lecturerId: string;
  scope?: RubricScope;
}) {
  assertValidFileExtension(params.file.name, [".docx"]);

  const form = new FormData();
  form.set("SubjectId", params.subjectId);
  if (params.assignmentId) {
    form.set("AssignmentId", params.assignmentId);
  }
  form.set("Name", params.file.name);
  form.set("File", params.file);
  form.set("Scope", params.scope ?? "lecturer");

  return apiPostForm<RubricListItem>("/catalog/rubrics/upload", form);
}

export async function listRubrics(params?: { subjectId?: string; assignmentId?: string | null }) {
  const query = new URLSearchParams();
  if (params?.subjectId) {
    query.set("subjectId", params.subjectId);
  }

  if (params?.assignmentId) {
    query.set("assignmentId", params.assignmentId);
  }

  const qs = query.toString();
  const rubrics = await apiGet<RubricListItem[]>(`/catalog/rubrics${qs ? `?${qs}` : ""}`);
  return [...rubrics].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export async function downloadRubricFile(rubric: RubricListItem) {
  const blob = await apiGetBlob(`/catalog/rubrics/${rubric.id}/file`);
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = rubric.name;
  link.click();
  URL.revokeObjectURL(url);
}

export function confirmRubric(rubricId: string) {
  return apiPost<RubricListItem>(`/catalog/rubrics/${rubricId}/confirm`);
}

export function unlockRubric(rubricId: string) {
  return apiPost<RubricListItem>(`/catalog/rubrics/${rubricId}/unlock`);
}

export function updateRubricCriteria(rubricId: string, criteria: RubricCriterionInput[]) {
  return apiPatch<RubricCriterion[]>(`/catalog/rubrics/${rubricId}/criteria`, criteria);
}
