import { apiGet } from "../lib/apiClient";

type Subject = {
  id: string;
  code: string;
  name: string;
  createdAt: string;
};

type Assignment = {
  id: string;
  subjectId: string;
  title: string;
  description?: string | null;
  dueDate?: string | null;
  createdAt: string;
};

export async function listSubjects() {
  const subjects = await apiGet<Subject[]>("/catalog/subjects");
  return [...subjects].sort((a, b) => a.code.localeCompare(b.code));
}

export async function listAssignments(subjectId: string) {
  const assignments = await apiGet<Assignment[]>(`/catalog/assignments?subjectId=${subjectId}`);
  return [...assignments].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}
