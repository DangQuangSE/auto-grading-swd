import { apiGet, apiPatch, apiPost } from "../lib/apiClient";

export type ClassSummary = {
  id: string;
  name: string;
  lecturerId: string | null;
};

export type Lecturer = {
  id: string;
  email: string;
  fullName: string;
};

export async function getClasses() {
  return apiGet<ClassSummary[]>("/catalog/classes");
}

export async function createClass(params: { name: string; lecturerId: string }) {
  return apiPost<ClassSummary>("/catalog/classes", { name: params.name, lecturerId: params.lecturerId });
}

export async function updateClassLecturer(classId: string, lecturerId: string) {
  return apiPatch<ClassSummary>(`/catalog/classes/${classId}`, { lecturerId });
}

export async function fetchLecturers() {
  const users = await apiGet<Array<{ id: string; email: string; fullName: string; role: string }>>("/identity/users");
  return users
    .filter((user) => user.role === "lecturer")
    .map((user): Lecturer => ({ id: user.id, email: user.email, fullName: user.fullName }));
}
