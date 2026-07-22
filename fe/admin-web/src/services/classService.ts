import { apiGet, apiPatch, apiPost } from "../lib/apiClient";
import type { PagedResult } from "../lib/pagination";

export type ClassSummary = {
  id: string;
  name: string;
  lecturerId: string;
  subjectId?: string | null;
  subjectCode?: string | null;
};

export type Lecturer = {
  id: string;
  email: string;
  fullName: string;
};

export async function getClasses() {
  const first = await apiGet<PagedResult<ClassSummary>>("/catalog/classes/admin?page=1&pageSize=100");
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page += 1) {
    const next = await apiGet<PagedResult<ClassSummary>>(`/catalog/classes/admin?page=${page}&pageSize=100`);
    items.push(...next.items);
  }
  return items;
}

export async function createClass(params: { name: string; lecturerId: string; subjectId: string }) {
  return apiPost<ClassSummary>("/catalog/classes/subject-scoped", params);
}

export async function updateClass(
  classId: string,
  changes: { lecturerId?: string; subjectId?: string },
) {
  return apiPatch<ClassSummary>(`/catalog/classes/${classId}`, changes);
}

export async function updateClassLecturer(classId: string, lecturerId: string) {
  return updateClass(classId, { lecturerId });
}

export async function fetchLecturers() {
  const users = await apiGet<Array<{ id: string; email: string; fullName: string; role: string }>>("/identity/users");
  return users
    .filter((user) => user.role === "lecturer")
    .map((user): Lecturer => ({ id: user.id, email: user.email, fullName: user.fullName }));
}
