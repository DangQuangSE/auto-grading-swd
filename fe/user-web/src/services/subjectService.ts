import { apiGet } from "../lib/apiClient";

const MAX_PAGE_SIZE = 100;

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

type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export async function listSubjects() {
  const result = await apiGet<PagedResult<Subject>>(`/catalog/subjects?pageSize=${MAX_PAGE_SIZE}`);
  return [...result.items].sort((a, b) => a.code.localeCompare(b.code));
}

export async function listAssignments(subjectId: string) {
  const result = await apiGet<PagedResult<Assignment>>(
    `/catalog/assignments?subjectId=${subjectId}&pageSize=${MAX_PAGE_SIZE}`,
  );
  return [...result.items].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}
