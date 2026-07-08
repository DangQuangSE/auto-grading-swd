import { supabase } from "../lib/supabaseClient";
import type { AppRole } from "../lib/database.types";

export async function getCurrentUser() {
  const { data, error } = await supabase.auth.getUser();
  if (error) throw error;
  return data.user;
}

export async function signInWithEmail(email: string, password: string) {
  const { data, error } = await supabase.auth.signInWithPassword({ email, password });
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
