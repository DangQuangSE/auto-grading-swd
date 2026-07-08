import { serve } from "https://deno.land/std@0.224.0/http/server.ts";
import { parseAiGradingResponse } from "../_shared/aiResponseParser.ts";
import { requestOpenRouterGrading } from "../_shared/openrouter.ts";
import { createServiceClient, updateSubmissionState, writeAuditEvent } from "../_shared/storage.ts";

type GradeRequest = {
  submissionId: string;
  actorId?: string;
};

async function gradeSubmission(submissionId: string, actorId?: string) {
  const supabase = createServiceClient();

  const { data: submission, error: submissionError } = await supabase
    .from("submissions")
    .select("id,rubric_id")
    .eq("id", submissionId)
    .single();

  if (submissionError || !submission?.rubric_id) {
    throw new Error(submissionError?.message ?? "Submission with rubric_id was not found.");
  }

  const { data: criteria, error: criteriaError } = await supabase
    .from("rubric_criteria")
    .select("id,criterion_code,title,description,max_score,grading_guidance,deduction_notes")
    .eq("rubric_id", submission.rubric_id)
    .order("display_order", { ascending: true });

  if (criteriaError || !criteria || criteria.length === 0) {
    throw new Error(criteriaError?.message ?? "Rubric criteria were not found.");
  }

  const { data: artifacts, error: artifactsError } = await supabase
    .from("extracted_artifacts")
    .select("artifact_type,content,warnings")
    .eq("submission_id", submissionId);

  if (artifactsError || !artifacts) {
    throw new Error(artifactsError?.message ?? "Extracted artifacts were not found.");
  }

  const documentArtifact = artifacts.find((artifact) => artifact.artifact_type === "document");
  const diagramArtifact = artifacts.find((artifact) => artifact.artifact_type === "diagram");
  const extractionWarnings = artifacts.flatMap((artifact) =>
    Array.isArray(artifact.warnings) ? artifact.warnings : [],
  );

  await updateSubmissionState(submissionId, "grading");
  await writeAuditEvent({ actorId, submissionId, eventType: "ai_grading_started" });

  const { data: run, error: runError } = await supabase
    .from("ai_grading_runs")
    .insert({
      submission_id: submissionId,
      model: Deno.env.get("OPENROUTER_MODEL") ?? "deepseek/deepseek-chat",
      status: "running",
      request_metadata: {
        criteria_count: criteria.length,
        has_document_artifact: Boolean(documentArtifact),
        has_diagram_artifact: Boolean(diagramArtifact),
      },
    })
    .select("id")
    .single();

  if (runError || !run) {
    throw new Error(runError?.message ?? "Unable to create AI grading run.");
  }

  try {
    const openRouterResult = await requestOpenRouterGrading({
      criteria,
      documentArtifact: documentArtifact?.content ?? {},
      diagramArtifact: diagramArtifact?.content ?? {},
      extractionWarnings,
    });

    const parsed = parseAiGradingResponse(openRouterResult.content, criteria);

    const { error: scoreError } = await supabase.from("ai_criterion_scores").insert(
      parsed.criterionScores.map((score) => ({
        grading_run_id: run.id,
        submission_id: submissionId,
        rubric_criterion_id: score.criterionId,
        max_score: score.maxScore,
        suggested_score: score.suggestedScore,
        deductions: score.deductions,
        evidence: score.evidence,
        comment: score.comment,
        confidence: score.confidence,
      })),
    );

    if (scoreError) {
      throw new Error(scoreError.message);
    }

    await supabase
      .from("ai_grading_runs")
      .update({
        status: "completed",
        model: openRouterResult.model,
        request_metadata: openRouterResult.metadata,
        raw_response: openRouterResult.rawResponse,
        completed_at: new Date().toISOString(),
      })
      .eq("id", run.id);

    await updateSubmissionState(submissionId, "graded");
    await writeAuditEvent({
      actorId,
      submissionId,
      eventType: "ai_grading_completed",
      details: {
        gradingRunId: run.id,
        criteriaCount: parsed.criterionScores.length,
      },
    });

    return {
      gradingRunId: run.id,
      criteriaCount: parsed.criterionScores.length,
      overallComment: parsed.overallComment,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Unknown AI grading error.";

    await supabase
      .from("ai_grading_runs")
      .update({
        status: "failed",
        error_message: message,
        completed_at: new Date().toISOString(),
      })
      .eq("id", run.id);

    throw error;
  }
}

serve(async (request) => {
  let body: GradeRequest | undefined;

  try {
    body = (await request.json()) as GradeRequest;

    if (!body.submissionId) {
      return Response.json({ error: "submissionId is required." }, { status: 400 });
    }

    const result = await gradeSubmission(body.submissionId, body.actorId);
    return Response.json({ data: result });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Unknown grading error.";

    if (body?.submissionId) {
      await updateSubmissionState(body.submissionId, "failed", message);
      await writeAuditEvent({
        actorId: body.actorId,
        submissionId: body.submissionId,
        eventType: "ai_grading_failed",
        details: { message },
      });
    }

    return Response.json({ error: message }, { status: 500 });
  }
});
