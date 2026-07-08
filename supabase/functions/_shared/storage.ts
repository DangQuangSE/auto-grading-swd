import { createClient } from "npm:@supabase/supabase-js@2.45.4";

export function createServiceClient() {
  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

  if (!supabaseUrl || !serviceRoleKey) {
    throw new Error("SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY are required.");
  }

  return createClient(supabaseUrl, serviceRoleKey, {
    auth: {
      persistSession: false,
    },
  });
}

export async function downloadObject(bucket: string, path: string) {
  const supabase = createServiceClient();
  const { data, error } = await supabase.storage.from(bucket).download(path);

  if (error || !data) {
    throw new Error(error?.message ?? `Unable to download ${bucket}/${path}.`);
  }

  return data.arrayBuffer();
}

export async function upsertArtifact(submissionId: string, artifactType: string, content: unknown, warnings: string[]) {
  const supabase = createServiceClient();
  const { error } = await supabase.from("extracted_artifacts").upsert(
    {
      submission_id: submissionId,
      artifact_type: artifactType,
      content,
      warnings,
      parser_version: "v1",
    },
    {
      onConflict: "submission_id,artifact_type",
    },
  );

  if (error) {
    throw new Error(error.message);
  }
}

export async function updateSubmissionState(submissionId: string, state: string, failureReason?: string) {
  const supabase = createServiceClient();
  const { error } = await supabase
    .from("submissions")
    .update({
      state,
      failure_reason: failureReason ?? null,
    })
    .eq("id", submissionId);

  if (error) {
    throw new Error(error.message);
  }
}

export async function writeAuditEvent(params: {
  actorId?: string | null;
  submissionId?: string | null;
  eventType: string;
  details?: Record<string, unknown>;
}) {
  const supabase = createServiceClient();
  const { error } = await supabase.from("audit_events").insert({
    actor_id: params.actorId ?? null,
    submission_id: params.submissionId ?? null,
    event_type: params.eventType,
    details: params.details ?? {},
  });

  if (error) {
    throw new Error(error.message);
  }
}
