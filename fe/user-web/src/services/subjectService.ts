import { apiGet } from "../lib/apiClient";

const MAX_PAGE_SIZE = 100;

export type Subject = {
  id: string;
  code: string;
  name: string;
  createdAt: string;
  registrationStatus: "open" | "closed";
};

export type Assignment = {
  id: string;
  subjectId: string;
  title: string;
  description?: string | null;
  dueDate?: string | null;
  maxAttempts: number;
  createdAt: string;
};

type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export async function listSubjects() {
  const result = await apiGet<PagedResult<Subject>>(`/catalog/subjects?pageSize=${MAX_PAGE_SIZE}`);
  return [...result.items].sort((a, b) => a.code.localeCompare(b.code));
}

export async function listOpenSubjects() {
  const first = await apiGet<PagedResult<Subject>>("/catalog/subjects/open-for-registration?page=1&pageSize=100");
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page += 1) {
    const next = await apiGet<PagedResult<Subject>>(
      `/catalog/subjects/open-for-registration?page=${page}&pageSize=100`,
    );
    items.push(...next.items);
  }
  return items.sort((a, b) => a.code.localeCompare(b.code));
}

export async function listAssignments(subjectId?: string) {
  const url = subjectId 
    ? `/catalog/assignments?subjectId=${subjectId}&pageSize=${MAX_PAGE_SIZE}`
    : `/catalog/assignments?pageSize=${MAX_PAGE_SIZE}`;
  const result = await apiGet<PagedResult<Assignment>>(url);
  return [...result.items].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}
