import { supabase } from "../lib/supabaseClient";

export async function listSubjects() {
  const { data, error } = await supabase.from("subjects").select("*").order("code");
  if (error) throw error;
  return data;
}

export async function createSubject(params: { code: string; name: string; createdBy: string }) {
  const { data, error } = await supabase
    .from("subjects")
    .insert({
      code: params.code,
      name: params.name,
      created_by: params.createdBy,
    })
    .select()
    .single();

  if (error) throw error;
  return data;
}

export async function listAssignments(subjectId: string) {
  const { data, error } = await supabase
    .from("assignments")
    .select("*")
    .eq("subject_id", subjectId)
    .order("created_at", { ascending: false });

  if (error) throw error;
  return data;
}

export async function createAssignment(params: {
  subjectId: string;
  title: string;
  description?: string;
  createdBy: string;
}) {
  const { data, error } = await supabase
    .from("assignments")
    .insert({
      subject_id: params.subjectId,
      title: params.title,
      description: params.description ?? "",
      created_by: params.createdBy,
    })
    .select()
    .single();

  if (error) throw error;
  return data;
}
