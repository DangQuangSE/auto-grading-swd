import { useMemo, useState } from "react";
import { ClipboardCheck, Download } from "lucide-react";
import { FileDropzone } from "../components/FileDropzone";
import { RubricCriteriaPanel } from "../components/RubricCriteriaPanel";
import { Button } from "../components/ui/Button";
import { Field, SelectInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { StatusBadge } from "../components/ui/StatusBadge";
import { useRubrics, useUploadRubric } from "../hooks/useRubrics";
import { useAssignments, useSubjects, useAllAssignments } from "../hooks/useSubjects";
import { MAX_PAGE_SIZE } from "../lib/pagination";
import { useAuth } from "../providers/AuthProvider";
import { downloadRubricFile, type RubricListItem, type RubricScope } from "../services/rubricService";

export function RubricUploadPage() {
  const [file, setFile] = useState<File | null>(null);
  const [subjectId, setSubjectId] = useState("");
  const [assignmentId, setAssignmentId] = useState("");
  const [scope, setScope] = useState<RubricScope>("lecturer");
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [selectedRubricId, setSelectedRubricId] = useState<string | null>(null);
  const { session } = useAuth();
  const isAdmin = session?.user.role === "admin";
  const subjects = useSubjects({ pageSize: MAX_PAGE_SIZE });
  const assignments = useAssignments(subjectId, { pageSize: MAX_PAGE_SIZE });
  const allAssignments = useAllAssignments();
  const rubrics = useRubrics(subjectId, assignmentId || null);
  const uploadRubric = useUploadRubric();
  const subjectsById = useMemo(
    () => new Map((subjects.data?.items ?? []).map((subject) => [subject.id, subject])),
    [subjects.data],
  );
  const assignmentsById = useMemo(
    () => new Map((allAssignments.data?.items ?? []).map((assignment) => [assignment.id, assignment])),
    [allAssignments.data],
  );
  const selectedRubric = (rubrics.data ?? []).find((rubric) => rubric.id === selectedRubricId) ?? null;

  function handleSubjectChange(nextSubjectId: string) {
    setSubjectId(nextSubjectId);
    setAssignmentId("");
  }

  async function handleDownload(rubric: RubricListItem) {
    setDownloadingId(rubric.id);
    try {
      await downloadRubricFile(rubric);
    } finally {
      setDownloadingId(null);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!file || !subjectId || !assignmentId || !session) {
      return;
    }

    const created = await uploadRubric.mutateAsync({
      subjectId,
      assignmentId,
      file,
      lecturerId: session.user.id,
      scope,
    });
    setFile(null);
    setScope("lecturer");
    setSelectedRubricId(created.id);
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Rubric</p>
        <h1>Upload subject criteria</h1>
      </header>
      <form className="form-panel" onSubmit={handleSubmit}>
        <Field label="Subject">
          <SelectInput value={subjectId} onChange={(event) => handleSubjectChange(event.target.value)} required>
            <option value="">Select subject</option>
            {(subjects.data?.items ?? []).map((subject) => (
              <option key={subject.id} value={subject.id}>
                {subject.code} - {subject.name}
              </option>
            ))}
          </SelectInput>
        </Field>
        <Field label="Assignment">
          <SelectInput
            value={assignmentId}
            onChange={(event) => setAssignmentId(event.target.value)}
            disabled={!subjectId}
            required
          >
            <option value="">Select assignment</option>
            {(assignments.data?.items ?? []).map((assignment) => (
              <option key={assignment.id} value={assignment.id}>
                {assignment.title}
              </option>
            ))}
          </SelectInput>
        </Field>
        <FileDropzone label="Rubric Word file" accept=".docx" file={file} onChange={setFile} />
        <fieldset className="radio-group">
          <legend>Scope</legend>
          <label>
            <input
              type="radio"
              name="scope"
              value="lecturer"
              checked={scope === "lecturer"}
              onChange={() => setScope("lecturer")}
            />
            Lecturer (only me)
          </label>
          <label>
            <input
              type="radio"
              name="scope"
              value="schoolWide"
              checked={scope === "schoolWide"}
              disabled={!isAdmin}
              onChange={() => setScope("schoolWide")}
            />
            School-wide {isAdmin ? "" : "(admin only)"}
          </label>
        </fieldset>
        {subjects.error ? <FormMessage tone="error">{subjects.error.message}</FormMessage> : null}
        {uploadRubric.error ? <FormMessage tone="error">{uploadRubric.error.message}</FormMessage> : null}
        {uploadRubric.isSuccess ? <FormMessage tone="success">Rubric uploaded and parsed successfully.</FormMessage> : null}
        <Button type="submit" disabled={!file || !subjectId || !assignmentId || uploadRubric.isPending}>
          <ClipboardCheck aria-hidden="true" />
          {uploadRubric.isPending ? "Uploading..." : "Parse rubric"}
        </Button>
      </form>
      <div className="table-panel">
        {rubrics.isLoading ? <StateBlock title="Loading rubrics" /> : null}
        {(rubrics.data ?? []).length === 0 && !rubrics.isLoading ? (
          <StateBlock title="No rubrics uploaded" detail="Upload a Word rubric to create criteria for a subject." />
        ) : null}
        {(rubrics.data ?? []).length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Subject</th>
                <th>Assignment</th>
                <th>Status</th>
                <th>Uploaded</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {(rubrics.data ?? []).map((rubric) => (
                <tr key={rubric.id}>
                  <td>{rubric.name}</td>
                  <td>{subjectsById.get(rubric.subjectId)?.code ?? "-"}</td>
                  <td>{rubric.assignmentId ? assignmentsById.get(rubric.assignmentId)?.title ?? "-" : "-"}</td>
                  <td>
                    <StatusBadge status={rubric.status} />
                  </td>
                  <td>{new Date(rubric.createdAt).toLocaleString()}</td>
                  <td>
                    <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                      <Button variant="text" onClick={() => setSelectedRubricId(rubric.id)}>
                        Manage
                      </Button>
                      <Button
                        variant="text"
                        onClick={() => handleDownload(rubric)}
                        disabled={downloadingId === rubric.id}
                      >
                        <Download aria-hidden="true" />
                        {downloadingId === rubric.id ? "Downloading..." : "Download"}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>
      {selectedRubric ? <RubricCriteriaPanel key={selectedRubric.id} rubric={selectedRubric} /> : null}
    </section>
  );
}
