import { apiGet, apiPost } from "../lib/apiClient";
import { DEFAULT_PAGE, DEFAULT_PAGE_SIZE, type PagedResult } from "../lib/pagination";

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

export async function listSubjects(params: { page?: number; pageSize?: number; search?: string } = {}) {
  const page = params.page ?? DEFAULT_PAGE;
  const pageSize = params.pageSize ?? DEFAULT_PAGE_SIZE;
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (params.search?.trim()) {
    query.set("search", params.search.trim());
  }

  return apiGet<PagedResult<Subject>>(`/catalog/subjects?${query.toString()}`);
}

export async function createSubject(params: { code: string; name: string; createdBy: string }) {
  return apiPost<Subject>("/catalog/subjects", { code: params.code, name: params.name });
}

export async function listAssignments(subjectId: string) {
  const assignments = await apiGet<Assignment[]>(`/catalog/assignments?subjectId=${subjectId}`);
  return [...assignments].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export async function createAssignment(params: {
  subjectId: string;
  title: string;
  description?: string;
  createdBy: string;
}) {
  return apiPost<Assignment>("/catalog/assignments", {
    subjectId: params.subjectId,
    title: params.title,
    description: params.description ?? "",
  });
}
