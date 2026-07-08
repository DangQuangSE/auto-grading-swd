import { triggerAiGrading, triggerExtraction } from "../services/gradingService";
import { uploadRubricDocx } from "../services/rubricService";
import { createSubmission } from "../services/submissionService";

export async function prepareRubricWorkflow(params: {
  subjectId: string;
  assignmentId?: string | null;
  file: File;
  lecturerId: string;
}) {
  return uploadRubricDocx(params);
}

export async function submitAndGradeWorkflow(params: {
  assignmentId: string;
  studentId: string;
  lecturerId?: string;
  rubricId?: string | null;
  reportFile: File;
  diagramFile: File;
}) {
  const submission = await createSubmission({
    assignmentId: params.assignmentId,
    studentId: params.studentId,
    rubricId: params.rubricId,
    reportFile: params.reportFile,
    diagramFile: params.diagramFile,
  });

  await triggerExtraction(submission.id, params.lecturerId ?? params.studentId);
  await triggerAiGrading(submission.id, params.lecturerId ?? params.studentId);

  return submission;
}

export async function retryGradingWorkflow(params: {
  submissionId: string;
  actorId: string;
  includeExtraction?: boolean;
}) {
  if (params.includeExtraction ?? true) {
    await triggerExtraction(params.submissionId, params.actorId);
  }

  return triggerAiGrading(params.submissionId, params.actorId);
}
