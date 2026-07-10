import { assertValidFileExtension } from "../lib/validation";
import { supabase } from "../lib/supabaseClient";

export type RubricListItem = {
  id: string;
  original_filename: string;
  status: string;
  version: number;
};

function rubricPath(subjectId: string, assignmentId: string | null | undefined, file: File) {
  const scope = assignmentId ?? "subject";
  return `${subjectId}/${scope}/${crypto.randomUUID()}-${file.name}`;
}

export async function uploadRubricDocx(params: {
  subjectId: string;
  assignmentId?: string | null;
  file: File;
  lecturerId: string;
}) {
  assertValidFileExtension(params.file.name, [".docx"]);

  const path = rubricPath(params.subjectId, params.assignmentId, params.file);
  const upload = await supabase.storage.from("rubrics").upload(path, params.file, {
    contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  });

  if (upload.error) throw upload.error;

  const { data, error } = await supabase
    .from("rubrics")
    .insert({
      subject_id: params.subjectId,
      assignment_id: params.assignmentId ?? null,
      file_path: path,
      original_filename: params.file.name,
      created_by: params.lecturerId,
    })
    .select()
    .single();

  if (error) throw error;

  await supabase.functions.invoke("extract-submission", {
    body: {
      rubricId: data.id,
      actorId: params.lecturerId,
    },
  });

  return data;
}

export async function listRubrics(subjectId: string) {
  const { data, error } = await supabase
    .from("rubrics")
    .select("id,original_filename,status,version")
    .eq("subject_id", subjectId)
    .order("created_at", { ascending: false });

  if (error) throw error;
  return data as RubricListItem[];
}
