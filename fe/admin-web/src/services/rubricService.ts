import { assertValidFileExtension } from "../lib/validation";
import { apiGet, apiGetBlob, apiPostForm } from "../lib/apiClient";

export type RubricListItem = {
  id: string;
  subjectId: string;
  assignmentId?: string | null;
  name: string;
  fileObjectKey?: string | null;
  createdAt: string;
};

export async function uploadRubricDocx(params: {
  subjectId: string;
  assignmentId?: string | null;
  file: File;
  lecturerId: string;
}) {
  assertValidFileExtension(params.file.name, [".docx"]);

  const form = new FormData();
  form.set("SubjectId", params.subjectId);
  if (params.assignmentId) {
    form.set("AssignmentId", params.assignmentId);
  }
  form.set("Name", params.file.name);
  form.set("File", params.file);

  return apiPostForm<RubricListItem>("/catalog/rubrics/upload", form);
}

export async function listRubrics() {
  const rubrics = await apiGet<RubricListItem[]>("/catalog/rubrics");
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
