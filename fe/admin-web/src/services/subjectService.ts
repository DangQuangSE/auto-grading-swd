import { apiGet, apiPatch, apiPost } from "../lib/apiClient";
import { DEFAULT_PAGE, DEFAULT_PAGE_SIZE, type PagedResult } from "../lib/pagination";

export type Subject = {
  id: string;
  code: string;
  name: string;
  registrationStatus: "open" | "closed";
  createdAt: string;
};

export type Assignment = {
  id: string;
  subjectId: string;
  title: string;
  description?: string | null;
  dueDate?: string | null;
  maxAttempts?: number;
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

export async function listAllSubjects() {
  const first = await listSubjects({ page: 1, pageSize: 100 });
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page += 1) {
    const next = await listSubjects({ page, pageSize: 100 });
    items.push(...next.items);
  }
  return items;
}

export async function updateSubjectRegistration(subjectId: string, status: "open" | "closed") {
  return apiPatch<Subject>(`/catalog/subjects/${subjectId}/registration`, { status });
}

export async function listAssignments(params: { subjectId?: string; page?: number; pageSize?: number }) {
  const page = params.page ?? DEFAULT_PAGE;
  const pageSize = params.pageSize ?? DEFAULT_PAGE_SIZE;
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  
  if (params.subjectId) {
    query.set("subjectId", params.subjectId);
  }

  return apiGet<PagedResult<Assignment>>(`/catalog/assignments?${query.toString()}`);
}

export async function createAssignment(params: {
  subjectId: string;
  title: string;
  description?: string;
  dueDate?: string;
  createdBy: string;
  maxAttempts: number;
}) {
  return apiPost<Assignment>("/catalog/assignments", {
    subjectId: params.subjectId,
    title: params.title,
    description: params.description ?? "",
    dueDate: params.dueDate ?? null,
    maxAttempts: params.maxAttempts,
  });
}
