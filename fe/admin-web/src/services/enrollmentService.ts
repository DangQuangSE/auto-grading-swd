import { apiGet, apiPut } from "../lib/apiClient";
import type { PagedResult } from "../lib/pagination";

export type AdminEnrollment = {
  id: string;
  studentId: string;
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

export async function listEnrollments(studentId: string) {
  const query = new URLSearchParams({ studentId, pageSize: "100" });
  return apiGet<PagedResult<AdminEnrollment>>(`/catalog/enrollments/admin?${query.toString()}`);
}

export async function correctEnrollment(params: {
  studentId: string;
  subjectId: string;
  classId: string;
  rowVersion: string;
}) {
  return apiPut<AdminEnrollment>(
    `/catalog/enrollments/admin/${params.studentId}/${params.subjectId}`,
    { classId: params.classId, rowVersion: params.rowVersion },
  );
}
