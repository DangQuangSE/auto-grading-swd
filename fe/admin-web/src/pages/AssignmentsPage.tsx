import { useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "../components/ui/Button";
import { Field, SelectInput, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { Pagination } from "../components/ui/Pagination";
import { StateBlock } from "../components/ui/StateBlock";
import { useAssignments, useCreateAssignment, useSubjects } from "../hooks/useSubjects";
import { DEFAULT_PAGE, DEFAULT_PAGE_SIZE, MAX_PAGE_SIZE } from "../lib/pagination";
import { useAuth } from "../providers/AuthProvider";

export function AssignmentsPage() {
  const [subjectId, setSubjectId] = useState("");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [dueDate, setDueDate] = useState("");
  const [maxAttempts, setMaxAttempts] = useState(1);
  const [page, setPage] = useState(DEFAULT_PAGE);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const { session } = useAuth();
  const subjects = useSubjects({ pageSize: MAX_PAGE_SIZE });
  const assignments = useAssignments(subjectId, { page, pageSize });
  const createAssignment = useCreateAssignment();

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!subjectId || !title.trim() || !session) {
      return;
    }

    await createAssignment.mutateAsync({
      subjectId,
      title: title.trim(),
      description: description.trim() || undefined,
      dueDate: dueDate || undefined,
      createdBy: session.user.id,
      maxAttempts,
    });
    setTitle("");
    setDescription("");
    setDueDate("");
    setPage(DEFAULT_PAGE);
  }

  function handlePageSizeChange(nextPageSize: number) {
    setPageSize(nextPageSize);
    setPage(DEFAULT_PAGE);
  }

  function handleSubjectChange(nextSubjectId: string) {
    setSubjectId(nextSubjectId);
    setPage(DEFAULT_PAGE);
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Assignments</p>
        <h1>Manage assignments</h1>
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
        <Field label="Assignment title">
          <TextInput
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            placeholder="Final exam, Group project #1..."
            required
          />
        </Field>
        <Field label="Description (optional)">
          <TextInput
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Details for this assignment"
          />
        </Field>
        <Field label="Due date (optional)">
          <TextInput type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} />
        </Field>
        <Field label="Maximum submission attempts">
          <TextInput type="number" min="1" value={String(maxAttempts)} onChange={(event) => setMaxAttempts(Math.max(1, Number(event.target.value)))} required />
        </Field>
        {createAssignment.error ? <FormMessage tone="error">{createAssignment.error.message}</FormMessage> : null}
        <Button type="submit" disabled={!subjectId || !title.trim() || createAssignment.isPending}>
          <Plus aria-hidden="true" />
          {createAssignment.isPending ? "Creating..." : "Create assignment"}
        </Button>
      </form>
      <div className="table-panel">
        {assignments.isLoading ? <StateBlock title="Loading assignments" /> : null}
        {assignments.error ? <FormMessage tone="error">{assignments.error.message}</FormMessage> : null}
        {assignments.data && assignments.data.items.length === 0 ? (
          <StateBlock title="No assignments yet" detail={subjectId ? "Create an assignment to start uploading rubrics for it." : "Select a subject to create your first assignment."} />
        ) : null}
        {assignments.data && assignments.data.items.length > 0 ? (
          <>
            <table>
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Description</th>
                  <th>Due date</th>
                  <th>Attempts</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {assignments.data.items.map((assignment) => (
                  <tr key={assignment.id}>
                    <td>{assignment.title}</td>
                    <td>{assignment.description || "-"}</td>
                    <td>{assignment.dueDate ? new Date(assignment.dueDate).toLocaleDateString() : "-"}</td>
                    <td>{assignment.maxAttempts ?? 1}</td>
                    <td>{new Date(assignment.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination
              page={assignments.data.page}
              pageSize={assignments.data.pageSize}
              totalCount={assignments.data.totalCount}
              totalPages={assignments.data.totalPages}
              onPageChange={setPage}
              onPageSizeChange={handlePageSizeChange}
            />
          </>
        ) : null}
      </div>
    </section>
  );
}
