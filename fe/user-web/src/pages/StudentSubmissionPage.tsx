import { useState } from "react";
import { Send } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { FileDropzone } from "../components/FileDropzone";
import { Button } from "../components/ui/Button";
import { Field, SelectInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { useCreateSubmission, useRunAiGrading, useRunExtraction } from "../hooks/useSubmissions";
import { useAssignments, useSubjects } from "../hooks/useSubjects";
import { useAuth } from "../providers/AuthProvider";

export function StudentSubmissionPage() {
  const [report, setReport] = useState<File | null>(null);
  const [diagram, setDiagram] = useState<File | null>(null);
  const [subjectId, setSubjectId] = useState("");
  const [assignmentId, setAssignmentId] = useState("");
  const { session } = useAuth();
  const navigate = useNavigate();
  const subjects = useSubjects();
  const assignments = useAssignments(subjectId);
  const createSubmission = useCreateSubmission();
  const runExtraction = useRunExtraction();
  const runAiGrading = useRunAiGrading();

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session || !report || !assignmentId) {
      return;
    }

    const submission = await createSubmission.mutateAsync({
      assignmentId,
      studentId: session.user.id,
      reportFile: report,
      diagramFile: diagram ?? undefined,
    });

    await runExtraction.mutateAsync({ submissionId: submission.id, actorId: session.user.id });
    await runAiGrading.mutateAsync({ submissionId: submission.id, actorId: session.user.id });

    navigate(`/result/${submission.id}`);
  }

  const isSubmitting = createSubmission.isPending || runExtraction.isPending || runAiGrading.isPending;

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Student</p>
        <h1>Submit project files</h1>
      </header>
      <form className="form-panel" onSubmit={handleSubmit}>
        <Field label="Subject">
          <SelectInput value={subjectId} onChange={(event) => setSubjectId(event.target.value)} required>
            <option value="">Select subject</option>
            {(subjects.data ?? []).map((subject) => (
              <option key={subject.id} value={subject.id}>
                {subject.code} - {subject.name}
              </option>
            ))}
          </SelectInput>
        </Field>
        <Field label="Assignment">
          <SelectInput value={assignmentId} onChange={(event) => setAssignmentId(event.target.value)} required>
            <option value="">Select assignment</option>
            {(assignments.data ?? []).map((assignment) => (
              <option key={assignment.id} value={assignment.id}>
                {assignment.title}
              </option>
            ))}
          </SelectInput>
        </Field>
        <FileDropzone label="Report document" accept=".docx" file={report} onChange={setReport} />
        <FileDropzone label="Architecture diagram (optional)" accept=".drawio" file={diagram} onChange={setDiagram} />
        {createSubmission.error ? <FormMessage tone="error">{createSubmission.error.message}</FormMessage> : null}
        {runExtraction.error ? <FormMessage tone="error">{runExtraction.error.message}</FormMessage> : null}
        {runAiGrading.error ? <FormMessage tone="error">{runAiGrading.error.message}</FormMessage> : null}
        {runAiGrading.isSuccess ? <FormMessage tone="success">Submission uploaded and AI grading started.</FormMessage> : null}
        <Button type="submit" disabled={!report || !assignmentId || isSubmitting}>
          <Send aria-hidden="true" />
          {isSubmitting ? "Submitting..." : "Submit"}
        </Button>
      </form>
    </section>
  );
}
