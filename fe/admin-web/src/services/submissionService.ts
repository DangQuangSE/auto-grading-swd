import { assertValidFileExtension } from "../lib/validation";
import { apiGet, apiPostForm } from "../lib/apiClient";
import type { GradingState } from "../lib/database.types";

export type SubmissionRecord = {
  id: string;
  assignmentId: string;
  studentId: string;
  reportObjectKey: string;
  diagramObjectKey: string;
  state: GradingState;
  createdAt: string;
  updatedAt: string;
};

export async function createSubmission(params: {
  assignmentId: string;
  studentId: string;
  reportFile: File;
  diagramFile: File;
}) {
  assertValidFileExtension(params.reportFile.name, [".docx"]);
  assertValidFileExtension(params.diagramFile.name, [".drawio"]);

  const form = new FormData();
  form.set("AssignmentId", params.assignmentId);
  form.set("StudentId", params.studentId);
  form.set("ReportFile", params.reportFile);
  form.set("DiagramFile", params.diagramFile);

  return apiPostForm<SubmissionRecord>("/submissions/submissions/upload", form);
}

export async function listMySubmissions(studentId: string) {
  const submissions = await apiGet<SubmissionRecord[]>(`/submissions/submissions?studentId=${studentId}`);
  return [...submissions].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export async function listAssignmentSubmissions(assignmentId: string) {
  const submissions = await apiGet<SubmissionRecord[]>(`/submissions/submissions?assignmentId=${assignmentId}`);
  return [...submissions].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}
