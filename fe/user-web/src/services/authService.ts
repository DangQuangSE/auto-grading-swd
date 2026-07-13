import { apiPost, clearStoredSession, getStoredSession, setStoredSession, type AppSession } from "../lib/apiClient";
import type { AppRole } from "../lib/database.types";

export function isAllowedEducationEmail(email?: string | null) {
  const domain = email?.trim().toLowerCase().split("@")[1];
  if (!domain) {
    return false;
  }
  return domain.endsWith(".edu") || domain.includes(".edu.");
}

function assertAllowedEducationEmail(email: string) {
  if (!isAllowedEducationEmail(email)) {
    throw new Error("Only .edu email addresses can access this system.");
  }
}

type LoginResponse = {
  token: string;
  userId: string;
  email: string;
  role: AppRole;
};

function toSession(response: LoginResponse): AppSession {
  return {
    token: response.token,
    user: { id: response.userId, email: response.email, role: response.role },
  };
}

export function getCurrentSession(): AppSession | null {
  return getStoredSession();
}

export function getCurrentUser(): AppSession["user"] | null {
  return getStoredSession()?.user ?? null;
}

export async function signInWithEmail(email: string, password: string) {
  assertAllowedEducationEmail(email);

  const response = await apiPost<LoginResponse>("/identity/auth/login", { email, password });
  const session = toSession(response);
  setStoredSession(session);
  return session;
}

export async function signUpWithEmail(params: {
  email: string;
  password: string;
  fullName: string;
  role: AppRole;
  studentCode?: string;
  classId?: string;
}) {
  assertAllowedEducationEmail(params.email);

  await apiPost("/identity/auth/register", {
    email: params.email,
    password: params.password,
    fullName: params.fullName,
    role: params.role,
    studentCode: params.studentCode || null,
    classId: params.classId || null,
  });

  return signInWithEmail(params.email, params.password);
}

export async function signInWithGoogle(idToken: string) {
  const response = await apiPost<LoginResponse>("/identity/auth/google", { idToken });
  const session = toSession(response);
  setStoredSession(session);
  return session;
}

export async function signOut() {
  clearStoredSession();
}
