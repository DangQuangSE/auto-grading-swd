import { serve } from "https://deno.land/std@0.224.0/http/server.ts";
import { parseDocx } from "../_shared/docxParser.ts";
import { parseDrawioXml } from "../_shared/drawioParser.ts";
import { parseRubricFromDocx } from "../_shared/rubricParser.ts";
import {
  createServiceClient,
  downloadObject,
  updateSubmissionState,
  upsertArtifact,
  writeAuditEvent,
} from "../_shared/storage.ts";

type ExtractRequest = {
  submissionId?: string;
  rubricId?: string;
  actorId?: string;
};

async function extractRubric(rubricId: string) {
  const supabase = createServiceClient();
  const { data: rubric, error } = await supabase
    .from("rubrics")
    .select("id,file_path")
    .eq("id", rubricId)
    .single();

  if (error || !rubric) {
    throw new Error(error?.message ?? "Rubric was not found.");
  }

  const buffer = await downloadObject("rubrics", rubric.file_path);
  const parsedDocx = await parseDocx(buffer);
  const parsedRubric = parseRubricFromDocx(parsedDocx);

  const { error: deleteError } = await supabase.from("rubric_criteria").delete().eq("rubric_id", rubric.id);
  if (deleteError) {
    throw new Error(deleteError.message);
  }

  const { error: insertError } = await supabase.from("rubric_criteria").insert(
    parsedRubric.criteria.map((criterion) => ({
      rubric_id: rubric.id,
      criterion_code: criterion.criterionCode,
      title: criterion.title,
      description: criterion.description,
      max_score: criterion.maxScore,
      grading_guidance: criterion.gradingGuidance,
      deduction_notes: criterion.deductionNotes,
      display_order: criterion.displayOrder,
    })),
  );

  if (insertError) {
    throw new Error(insertError.message);
  }

  await supabase.from("rubrics").update({ status: "parsed" }).eq("id", rubric.id);

  return {
    rubricId,
    criteriaCount: parsedRubric.criteria.length,
    warnings: parsedRubric.warnings,
  };
}

async function extractSubmission(submissionId: string, actorId?: string) {
  const supabase = createServiceClient();
  const { data: submission, error } = await supabase
    .from("submissions")
    .select("id,report_file_path,diagram_file_path")
    .eq("id", submissionId)
    .single();

  if (error || !submission) {
    throw new Error(error?.message ?? "Submission was not found.");
  }

  await updateSubmissionState(submissionId, "extracting");
  await writeAuditEvent({ actorId, submissionId, eventType: "extraction_started" });

  const reportBuffer = await downloadObject("submissions", submission.report_file_path);
  const report = await parseDocx(reportBuffer);
  await upsertArtifact(
    submissionId,
    "document",
    {
      sections: report.sections,
    },
    report.warnings,
  );

  const diagramBuffer = await downloadObject("submissions", submission.diagram_file_path);
  const diagramText = new TextDecoder().decode(diagramBuffer);
  const diagram = parseDrawioXml(diagramText);
  await upsertArtifact(
    submissionId,
    "diagram",
    {
      entities: diagram.entities,
      relationships: diagram.relationships,
    },
    diagram.warnings,
  );

  await updateSubmissionState(submissionId, "extracted");
  await writeAuditEvent({
    actorId,
    submissionId,
    eventType: "extraction_completed",
    details: {
      documentWarnings: report.warnings,
      diagramWarnings: diagram.warnings,
    },
  });

  return {
    submissionId,
    documentSections: report.sections.length,
    diagramEntities: diagram.entities.length,
    diagramRelationships: diagram.relationships.length,
    warnings: [...report.warnings, ...diagram.warnings],
  };
}

serve(async (request) => {
  try {
    const body = (await request.json()) as ExtractRequest;

    if (!body.submissionId && !body.rubricId) {
      return Response.json({ error: "submissionId or rubricId is required." }, { status: 400 });
    }

    const result = body.rubricId
      ? await extractRubric(body.rubricId)
      : await extractSubmission(body.submissionId!, body.actorId);

    return Response.json({ data: result });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Unknown extraction error.";

    try {
      const body = (await request.clone().json()) as ExtractRequest;
      if (body.submissionId) {
        await updateSubmissionState(body.submissionId, "failed", message);
        await writeAuditEvent({
          actorId: body.actorId,
          submissionId: body.submissionId,
          eventType: "extraction_failed",
          details: { message },
        });
      }
    } catch {
      // The original error is more important than best-effort failure logging.
    }

    return Response.json({ error: message }, { status: 500 });
  }
});
