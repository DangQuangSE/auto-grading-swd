import type { AppRole } from "./database.types";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "http://localhost:5500";
const SESSION_STORAGE_KEY = "auto-grading.session";

export type AppSession = {
  token: string;
  user: {
    id: string;
    email: string;
    role: AppRole;
  };
};

export class ApiError extends Error {
  status: number;
  /** Parsed JSON body of the error response, if available. */
  body: unknown;

  constructor(status: number, message: string, body: unknown = null) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

export function getStoredSession(): AppSession | null {
  const raw = localStorage.getItem(SESSION_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as AppSession;
  } catch {
    return null;
  }
}

export function setStoredSession(session: AppSession): void {
  localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
}

export function clearStoredSession(): void {
  localStorage.removeItem(SESSION_STORAGE_KEY);
}

async function readErrorBody(response: Response): Promise<{ message: string; body: unknown }> {
  try {
    const body = (await response.json()) as { message?: string; title?: string };
    const message = (body as any).message ?? (body as any).title ?? response.statusText;
    return { message, body };
  } catch {
    return { message: response.statusText, body: null };
  }
}

async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (!(init.body instanceof FormData) && init.body != null) {
    headers.set("Content-Type", "application/json");
  }

  const token = getStoredSession()?.token;
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });

  if (!response.ok) {
    const { message, body } = await readErrorBody(response);
    throw new ApiError(response.status, message, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function apiGet<T>(path: string): Promise<T> {
  return apiRequest<T>(path, { method: "GET" });
}

export function apiPost<T>(path: string, body?: unknown): Promise<T> {
  return apiRequest<T>(path, {
    method: "POST",
    body: body != null ? JSON.stringify(body) : undefined,
  });
}

export function apiPut<T>(path: string, body?: unknown): Promise<T> {
  return apiRequest<T>(path, {
    method: "PUT",
    body: body != null ? JSON.stringify(body) : undefined,
  });
}

export function apiPostForm<T>(path: string, form: FormData): Promise<T> {
  return apiRequest<T>(path, { method: "POST", body: form });
}
