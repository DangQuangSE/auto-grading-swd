import { supabase } from "../lib/supabaseClient";
import type { AppRole } from "../lib/database.types";

export function isAllowedEducationEmail(email?: string | null) {
  return Boolean(email?.trim().toLowerCase().endsWith(".edu"));
}

function assertAllowedEducationEmail(email: string) {
  if (!isAllowedEducationEmail(email)) {
    throw new Error("Only .edu email addresses can access this system.");
  }
}

export async function getCurrentSession() {
  const { data, error } = await supabase.auth.getSession();
  if (error) throw error;
  return data.session;
}

export async function getCurrentUser() {
  const { data, error } = await supabase.auth.getUser();
  if (error) throw error;
  return data.user;
}

export async function signInWithEmail(email: string, password: string) {
  assertAllowedEducationEmail(email);

  const { data, error } = await supabase.auth.signInWithPassword({ email, password });
  if (error) throw error;
  return data;
}

export async function signUpWithEmail(params: {
  email: string;
  password: string;
  fullName: string;
  role: AppRole;
}) {
  assertAllowedEducationEmail(params.email);

  const { data, error } = await supabase.auth.signUp({
    email: params.email,
    password: params.password,
    options: {
      data: {
        full_name: params.fullName,
        role: params.role,
      },
    },
  });

  if (error) throw error;

  if (data.user) {
    await upsertProfile({
      id: data.user.id,
      email: params.email,
      fullName: params.fullName,
      role: params.role,
    });
  }

  return data;
}

export async function signInWithGoogle() {
  const { data, error } = await supabase.auth.signInWithOAuth({
    provider: "google",
    options: {
      redirectTo: window.location.origin,
      queryParams: {
        access_type: "offline",
        prompt: "select_account",
      },
    },
  });

  if (error) throw error;
  return data;
}

export async function signOut() {
  const { error } = await supabase.auth.signOut();
  if (error) throw error;
}

export async function getProfile(userId: string) {
  const { data, error } = await supabase.from("profiles").select("*").eq("id", userId).single();
  if (error) throw error;
  return data;
}

export async function upsertProfile(params: {
  id: string;
  email: string;
  fullName: string;
  role?: AppRole;
}) {
  const { data, error } = await supabase
    .from("profiles")
    .upsert({
      id: params.id,
      email: params.email,
      full_name: params.fullName,
      role: params.role ?? "student",
    })
    .select()
    .single();

  if (error) throw error;
  return data;
}
