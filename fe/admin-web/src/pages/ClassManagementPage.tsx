import { useState } from "react";
import { Plus, Search, Users } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "../components/ui/Button";
import { Field, SelectInput, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { useClasses, useCreateClass, useLecturers, useUpdateClass } from "../hooks/useClasses";
import { useAdminEnrollments, useCorrectEnrollment } from "../hooks/useEnrollments";
import { useAllSubjects } from "../hooks/useSubjects";
import { ApiError } from "../lib/apiClient";

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 403) return "You do not have permission to perform this action.";
    if (error.status === 409) return "The data changed. Refresh and try again.";
    if (error.status >= 500) return "Something went wrong. Please try again.";
    return error.message || "Request failed. Please try again.";
  }
  return "Something went wrong. Please try again.";
}

export function ClassManagementPage() {
  const [name, setName] = useState("");
  const [lecturerId, setLecturerId] = useState("");
  const [subjectId, setSubjectId] = useState("");
  const [editingClassId, setEditingClassId] = useState<string | null>(null);
  const [editLecturerId, setEditLecturerId] = useState("");
  const [editSubjectId, setEditSubjectId] = useState("");
  const [studentFilter, setStudentFilter] = useState("");
  const [activeStudentId, setActiveStudentId] = useState("");
  const [replacementByEnrollment, setReplacementByEnrollment] = useState<Record<string, string>>({});

  const classes = useClasses();
  const lecturers = useLecturers();
  const subjects = useAllSubjects();
  const createClass = useCreateClass();
  const updateClass = useUpdateClass();
  const enrollments = useAdminEnrollments(activeStudentId);
  const correctEnrollment = useCorrectEnrollment();
  const subjectItems = subjects.data ?? [];

  async function handleCreateSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!name.trim() || !lecturerId || !subjectId) return;
    try {
      await createClass.mutateAsync({ name: name.trim(), lecturerId, subjectId });
      setName("");
      setLecturerId("");
      setSubjectId("");
    } catch {
      // Mutation state renders the error.
    }
  }

  function startEdit(classId: string, currentLecturerId: string, currentSubjectId: string | null) {
    setEditingClassId(classId);
    setEditLecturerId(currentLecturerId);
    setEditSubjectId(currentSubjectId ?? "");
  }

  async function saveClass(classId: string) {
    if (!editLecturerId || !editSubjectId) return;
    try {
      await updateClass.mutateAsync({
        classId,
        changes: { lecturerId: editLecturerId, subjectId: editSubjectId },
      });
      setEditingClassId(null);
    } catch {
      // Mutation state renders the error.
    }
  }

  async function saveEnrollment(enrollment: NonNullable<typeof enrollments.data>["items"][number]) {
    const classId = replacementByEnrollment[enrollment.id];
    if (!classId) return;
    try {
      await correctEnrollment.mutateAsync({
        studentId: enrollment.studentId,
        subjectId: enrollment.subjectId,
        classId,
        rowVersion: enrollment.rowVersion,
      });
      setReplacementByEnrollment((current) => ({ ...current, [enrollment.id]: "" }));
    } catch {
      // The hook refreshes current data after conflicts.
    }
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Classes</p>
        <h1>Manage classes</h1>
        <Link to="/roster" className="secondary-button"><Users aria-hidden="true" />Student roster</Link>
      </header>

      <form className="form-panel" onSubmit={handleCreateSubmit}>
        <Field label="Class name"><TextInput value={name} onChange={(event) => setName(event.target.value)} placeholder="SE1801" required /></Field>
        <Field label="Subject">
          <SelectInput value={subjectId} onChange={(event) => setSubjectId(event.target.value)} required>
            <option value="" disabled>Select a subject</option>
            {subjectItems.map((subject) => <option key={subject.id} value={subject.id}>{subject.code} — {subject.name}</option>)}
          </SelectInput>
        </Field>
        <Field label="Lecturer">
          <SelectInput value={lecturerId} onChange={(event) => setLecturerId(event.target.value)} required>
            <option value="" disabled>Select a lecturer</option>
            {(lecturers.data ?? []).map((lecturer) => <option key={lecturer.id} value={lecturer.id}>{lecturer.fullName} ({lecturer.email})</option>)}
          </SelectInput>
        </Field>
        {createClass.error ? <FormMessage tone="error">{describeError(createClass.error)}</FormMessage> : null}
        <Button type="submit" disabled={!name.trim() || !lecturerId || !subjectId || createClass.isPending}>
          <Plus aria-hidden="true" />{createClass.isPending ? "Creating..." : "Create class"}
        </Button>
      </form>

      <div className="table-panel">
        {classes.isLoading ? <StateBlock title="Loading classes" /> : null}
        {classes.error ? <FormMessage tone="error">{describeError(classes.error)}</FormMessage> : null}
        {classes.data?.length === 0 ? <StateBlock title="No classes yet" detail="Create a class and assign its subject and lecturer." /> : null}
        {classes.data && classes.data.length > 0 ? (
          <table>
            <thead><tr><th>Name</th><th>Subject</th><th>Lecturer</th><th aria-label="Actions" /></tr></thead>
            <tbody>
              {classes.data.map((klass) => (
                <tr key={klass.id}>
                  <td>{klass.name}</td>
                  <td>{editingClassId === klass.id ? (
                    <SelectInput value={editSubjectId} onChange={(event) => setEditSubjectId(event.target.value)}>
                      <option value="" disabled>Select a subject</option>
                      {subjectItems.map((subject) => <option key={subject.id} value={subject.id}>{subject.code}</option>)}
                    </SelectInput>
                  ) : (klass.subjectCode ?? "Unassigned (legacy)")}</td>
                  <td>{editingClassId === klass.id ? (
                    <SelectInput value={editLecturerId} onChange={(event) => setEditLecturerId(event.target.value)}>
                      <option value="" disabled>Select a lecturer</option>
                      {(lecturers.data ?? []).map((lecturer) => <option key={lecturer.id} value={lecturer.id}>{lecturer.fullName}</option>)}
                    </SelectInput>
                  ) : ((lecturers.data ?? []).find((item) => item.id === klass.lecturerId)?.fullName ?? klass.lecturerId)}</td>
                  <td>{editingClassId === klass.id ? (
                    <><Button type="button" variant="secondary" disabled={!editLecturerId || !editSubjectId || updateClass.isPending} onClick={() => saveClass(klass.id)}>Save</Button><Button type="button" variant="text" onClick={() => setEditingClassId(null)}>Cancel</Button></>
                  ) : <Button type="button" variant="text" onClick={() => startEdit(klass.id, klass.lecturerId, klass.subjectId ?? null)}>Edit</Button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
        {updateClass.error ? <FormMessage tone="error">{describeError(updateClass.error)}</FormMessage> : null}
      </div>

      <div className="table-panel">
        <h2>Correct student enrollment</h2>
        <form onSubmit={(event) => { event.preventDefault(); setActiveStudentId(studentFilter.trim()); }}>
          <Field label="Student ID"><TextInput value={studentFilter} onChange={(event) => setStudentFilter(event.target.value)} placeholder="Student UUID" required /></Field>
          <Button type="submit" variant="secondary" disabled={!studentFilter.trim()}><Search aria-hidden="true" />Find enrollments</Button>
        </form>
        {enrollments.isLoading ? <StateBlock title="Loading enrollments" /> : null}
        {enrollments.error ? <FormMessage tone="error">{describeError(enrollments.error)}</FormMessage> : null}
        {enrollments.data?.items.length === 0 ? <StateBlock title="No enrollments found" /> : null}
        {enrollments.data && enrollments.data.items.length > 0 ? (
          <table>
            <thead><tr><th>Subject</th><th>Current class</th><th>Replacement</th><th aria-label="Actions" /></tr></thead>
            <tbody>{enrollments.data.items.map((enrollment) => (
              <tr key={enrollment.id}>
                <td>{enrollment.subjectCode} ({enrollment.registrationStatus})</td>
                <td>{enrollment.className}</td>
                <td><SelectInput value={replacementByEnrollment[enrollment.id] ?? ""} onChange={(event) => setReplacementByEnrollment((current) => ({ ...current, [enrollment.id]: event.target.value }))}>
                  <option value="" disabled>Select replacement</option>
                  {(classes.data ?? []).filter((item) => item.subjectId === enrollment.subjectId).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </SelectInput></td>
                <td><Button type="button" variant="secondary" disabled={!replacementByEnrollment[enrollment.id] || correctEnrollment.isPending} onClick={() => saveEnrollment(enrollment)}>Save</Button></td>
              </tr>
            ))}</tbody>
          </table>
        ) : null}
        {correctEnrollment.error ? <FormMessage tone="error">{describeError(correctEnrollment.error)}</FormMessage> : null}
      </div>
    </section>
  );
}
