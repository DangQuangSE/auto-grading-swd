// The backend runs extraction and AI grading automatically as background jobs
// (Hangfire handlers reacting to SubmissionUploaded/ArtifactsExtracted events),
// so there is no manual trigger endpoint. These are kept as no-ops so callers
// that used to kick off Supabase Edge Functions keep working unchanged.
export async function triggerExtraction(_submissionId: string, _actorId?: string) {
  return Promise.resolve(null);
}

export async function triggerAiGrading(_submissionId: string, _actorId?: string) {
  return Promise.resolve(null);
}
