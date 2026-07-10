import { useState } from "react";
import { ClipboardCheck } from "lucide-react";
import { FileDropzone } from "../components/FileDropzone";
import { Button } from "../components/ui/Button";
import { Field, SelectInput, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { useRubrics, useUploadRubric } from "../hooks/useRubrics";
import { useSubjects } from "../hooks/useSubjects";
import { useAuth } from "../providers/AuthProvider";

export function RubricUploadPage() {
  const [file, setFile] = useState<File | null>(null);
  const [subjectId, setSubjectId] = useState("");
  const [assignmentId, setAssignmentId] = useState("");
  const { session } = useAuth();
  const subjects = useSubjects();
  const rubrics = useRubrics(subjectId);
  const uploadRubric = useUploadRubric();

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!file || !subjectId || !session) {
      return;
    }

    await uploadRubric.mutateAsync({
      subjectId,
      assignmentId: assignmentId || null,
      file,
      lecturerId: session.user.id,
    });
    setFile(null);
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Rubric</p>
        <h1>Upload subject criteria</h1>
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
        <Field label="Assignment ID (optional)">
          <TextInput value={assignmentId} onChange={(event) => setAssignmentId(event.target.value)} placeholder="UUID" />
        </Field>
        <FileDropzone label="Rubric Word file" accept=".docx" file={file} onChange={setFile} />
        {subjects.error ? <FormMessage tone="error">{subjects.error.message}</FormMessage> : null}
        {uploadRubric.error ? <FormMessage tone="error">{uploadRubric.error.message}</FormMessage> : null}
        {uploadRubric.isSuccess ? <FormMessage tone="success">Rubric uploaded and parsing started.</FormMessage> : null}
        <Button type="submit" disabled={!file || !subjectId || uploadRubric.isPending}>
          <ClipboardCheck aria-hidden="true" />
          {uploadRubric.isPending ? "Uploading..." : "Parse rubric"}
        </Button>
      </form>
      {subjectId ? (
        <div className="table-panel">
          {rubrics.isLoading ? <StateBlock title="Loading rubrics" /> : null}
          {(rubrics.data ?? []).length === 0 && !rubrics.isLoading ? (
            <StateBlock title="No rubrics uploaded" detail="Upload a Word rubric to create criteria for this subject." />
          ) : null}
          {(rubrics.data ?? []).length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Uploaded</th>
                </tr>
              </thead>
              <tbody>
                {(rubrics.data ?? []).map((rubric) => (
                  <tr key={rubric.id}>
                    <td>{rubric.name}</td>
                    <td>{new Date(rubric.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
