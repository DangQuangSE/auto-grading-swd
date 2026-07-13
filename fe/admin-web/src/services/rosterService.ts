import { apiGet, apiPatch } from "../lib/apiClient";

export type RosterUser = {
  id: string;
  email: string;
  fullName: string;
  role: string;
  studentCode: string | null;
  classId: string | null;
  className: string | null;
};

export async function listUsers() {
  return apiGet<RosterUser[]>("/identity/users");
}

export async function getUsersByIds(userIds: string[]) {
  const dedupedIds = [...new Set(userIds)];
  if (dedupedIds.length === 0) {
    return [];
  }

  return apiGet<RosterUser[]>(`/identity/users?ids=${dedupedIds.join(",")}`);
}

export async function getUser(userId: string) {
  const users = await getUsersByIds([userId]);
  return users[0] ?? null;
}

export async function updateUser(userId: string, params: { studentCode?: string | null; classId?: string | null }) {
  return apiPatch<RosterUser>(`/identity/users/${userId}`, {
    studentCode: params.studentCode ?? null,
    classId: params.classId ?? null,
  });
}
