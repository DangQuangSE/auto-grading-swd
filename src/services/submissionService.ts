import { assertValidFileExtension } from "../lib/validation";
import { supabase } from "../lib/supabaseClient";

function submissionPath(assignmentId: string, studentId: string, file: File) {
  return `${assignmentId}/${studentId}/${crypto.randomUUID()}-${file.name}`;
}

export async function createSubmission(params: {
  assignmentId: string;
  studentId: string;
  rubricId?: string | null;
  reportFile: File;
  diagramFile: File;
}) {
  assertValidFileExtension(params.reportFile.name, [".docx"]);
  assertValidFileExtension(params.diagramFile.name, [".drawio"]);

  const reportPath = submissionPath(params.assignmentId, params.studentId, params.reportFile);
  const diagramPath = submissionPath(params.assignmentId, params.studentId, params.diagramFile);

  const reportUpload = await supabase.storage.from("submissions").upload(reportPath, params.reportFile, {
    contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  });
  if (reportUpload.error) throw reportUpload.error;

  const diagramUpload = await supabase.storage.from("submissions").upload(diagramPath, params.diagramFile, {
    contentType: "text/xml",
  });
  if (diagramUpload.error) throw diagramUpload.error;

  const { data, error } = await supabase
    .from("submissions")
    .insert({
      assignment_id: params.assignmentId,
      student_id: params.studentId,
      rubric_id: params.rubricId ?? null,
      report_file_path: reportPath,
      diagram_file_path: diagramPath,
      report_original_filename: params.reportFile.name,
      diagram_original_filename: params.diagramFile.name,
    })
    .select()
    .single();

  if (error) throw error;

  await supabase.from("audit_events").insert({
    actor_id: params.studentId,
    assignment_id: params.assignmentId,
    submission_id: data.id,
    event_type: "file_uploaded",
    details: {
      report: params.reportFile.name,
      diagram: params.diagramFile.name,
    },
  });

  return data;
}

export async function listMySubmissions(studentId: string) {
  const { data, error } = await supabase
    .from("submissions")
    .select("*")
    .eq("student_id", studentId)
    .order("submitted_at", { ascending: false });

  if (error) throw error;
  return data;
}

export async function listAssignmentSubmissions(assignmentId: string) {
  const { data, error } = await supabase
    .from("submissions")
    .select("*")
    .eq("assignment_id", assignmentId)
    .order("submitted_at", { ascending: false });

  if (error) throw error;
  return data;
}
