import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Send } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { FileDropzone } from "../components/FileDropzone";
import { Button } from "../components/ui/Button";
import { Field, SelectInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { useCreateSubmission } from "../hooks/useSubmissions";
import { useAssignments, useSubjects } from "../hooks/useSubjects";
import { useAuth } from "../providers/AuthProvider";
import { listMySubmissions } from "../services/submissionService";

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
  const submissions = useQuery({ queryKey: ["my-submissions", session?.user.id], queryFn: () => listMySubmissions(session!.user.id), enabled: Boolean(session) });
  const selectedAssignment = assignments.data?.find((assignment) => assignment.id === assignmentId);
  const usedAttempts = (submissions.data ?? []).filter((submission) => submission.assignmentId === assignmentId).length;
  const maxAttempts = selectedAssignment?.maxAttempts ?? 1;
  const limitReached = Boolean(assignmentId) && usedAttempts >= maxAttempts;

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

    navigate(`/result/${submission.id}`);
  }

  const isSubmitting = createSubmission.isPending;

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
        {assignmentId ? <FormMessage tone={limitReached ? "error" : "success"}>{`Attempts used: ${usedAttempts} / ${maxAttempts}`}</FormMessage> : null}
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
        <Button type="submit" disabled={!report || !assignmentId || isSubmitting || limitReached}>
          <Send aria-hidden="true" />
          {isSubmitting ? "Submitting..." : "Submit"}
        </Button>
      </form>
    </section>
  );
}
