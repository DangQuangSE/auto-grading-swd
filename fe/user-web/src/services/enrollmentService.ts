import { apiGet, apiPut } from "../lib/apiClient";

export type Enrollment = {
  id: string;
  subjectId: string;
  subjectCode: string;
  subjectName: string;
  registrationStatus: "open" | "closed";
  classId: string;
  className: string;
  rowVersion: string;
  createdAt: string;
  updatedAt: string;
};

type PagedResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export async function listMyEnrollments() {
  const first = await apiGet<PagedResult<Enrollment>>("/catalog/enrollments/me?page=1&pageSize=100");
  const items = [...first.items];
  for (let page = 2; page <= first.totalPages; page += 1) {
    const next = await apiGet<PagedResult<Enrollment>>(`/catalog/enrollments/me?page=${page}&pageSize=100`);
    items.push(...next.items);
  }
  return items;
}

export async function saveMyEnrollment(params: { subjectId: string; classId: string; rowVersion: string | null }) {
  return apiPut<Enrollment>(`/catalog/enrollments/me/${params.subjectId}`, {
    classId: params.classId,
    rowVersion: params.rowVersion,
  });
}
