import { apiGet } from "../lib/apiClient";

export type ClassOption = {
  id: string;
  name: string;
  subjectId?: string;
};

type PagedResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export async function getClasses() {
  return apiGet<ClassOption[]>("/catalog/classes");
}

export async function getClassesBySubject(subjectId: string) {
  const first = await apiGet<PagedResult<ClassOption>>(
    `/catalog/classes/by-subject/${subjectId}?page=1&pageSize=100`,
  );
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page += 1) {
    const next = await apiGet<PagedResult<ClassOption>>(
      `/catalog/classes/by-subject/${subjectId}?page=${page}&pageSize=100`,
    );
    items.push(...next.items);
  }
  return items;
}
