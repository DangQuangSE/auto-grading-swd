import { useMemo, useState } from "react";
import { Save } from "lucide-react";
import { Button } from "../components/ui/Button";
import { Field, SelectInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { Panel } from "../components/ui/Panel";
import { StateBlock } from "../components/ui/StateBlock";
import { useMyEnrollments, useOpenSubjects, useSaveMyEnrollment, useSubjectClasses } from "../hooks/useEnrollments";
import { ApiError } from "../lib/apiClient";
import { useAuth } from "../providers/AuthProvider";

function describeError(error: unknown) {
  if (error instanceof ApiError && error.status === 409) {
    return "Enrollment changed or registration closed. Current data was refreshed; review it before saving again.";
  }
  return error instanceof Error ? error.message : "Unable to save enrollment.";
}

export function StudentProfilePage() {
  const { session } = useAuth();
  const [subjectId, setSubjectId] = useState("");
  const [classId, setClassId] = useState("");
  const [successMessage, setSuccessMessage] = useState("");
  const [conflictSubjectId, setConflictSubjectId] = useState<string | null>(null);
  const [conflictConfirmed, setConflictConfirmed] = useState(false);
  const subjects = useOpenSubjects();
  const classes = useSubjectClasses(subjectId);
  const enrollments = useMyEnrollments();
  const saveEnrollment = useSaveMyEnrollment();

  const selectedEnrollment = useMemo(
    () => enrollments.data?.find((item) => item.subjectId === subjectId),
    [enrollments.data, subjectId],
  );

  function changeSubject(nextSubjectId: string) {
    setSubjectId(nextSubjectId);
    setClassId("");
    setSuccessMessage("");
    setConflictSubjectId(null);
    setConflictConfirmed(false);
  }

  async function handleSave(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!subjectId || !classId) return;
    setSuccessMessage("");
    try {
      await saveEnrollment.mutateAsync({
        subjectId,
        classId,
        rowVersion: selectedEnrollment?.rowVersion ?? null,
      });
      setSuccessMessage("Enrollment saved.");
      setConflictSubjectId(null);
      setConflictConfirmed(false);
      setSubjectId("");
      setClassId("");
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setConflictSubjectId(subjectId);
        setConflictConfirmed(false);
      }
      // Mutation state renders the error and the hook refreshes current data.
    }
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Profile</p>
        <h1>Personal information</h1>
      </header>

      <Panel>
        <h2>Account</h2>
        <p>{session?.user.email ?? "Signed in"}</p>
      </Panel>

      <Panel>
        <h2>Subjects and classes</h2>
        {enrollments.isLoading ? <StateBlock title="Loading enrollments" /> : null}
        {enrollments.error ? <FormMessage tone="error">{enrollments.error.message}</FormMessage> : null}
        {enrollments.data?.length === 0 ? <StateBlock title="No enrolled subjects" detail="Choose an open subject below." /> : null}
        {enrollments.data && enrollments.data.length > 0 ? (
          <table>
            <thead><tr><th>Subject</th><th>Class</th><th>Status</th></tr></thead>
            <tbody>{enrollments.data.map((enrollment) => (
              <tr key={enrollment.id}>
                <td>{enrollment.subjectCode} — {enrollment.subjectName}</td>
                <td>{enrollment.className}</td>
                <td>{enrollment.registrationStatus === "open" ? "Open for changes" : "Closed (read only)"}</td>
              </tr>
            ))}</tbody>
          </table>
        ) : null}
      </Panel>

      <form className="form-panel" onSubmit={handleSave}>
        <h2>Choose or change class</h2>
        {subjects.isLoading ? <StateBlock title="Loading open subjects" /> : null}
        <Field label="Subject">
          <SelectInput value={subjectId} onChange={(event) => changeSubject(event.target.value)} required>
            <option value="" disabled>Select an open subject</option>
            {(subjects.data ?? []).map((subject) => <option key={subject.id} value={subject.id}>{subject.code} — {subject.name}</option>)}
          </SelectInput>
        </Field>
        <Field label="Class">
          <SelectInput value={classId} onChange={(event) => { setClassId(event.target.value); setConflictSubjectId(null); setConflictConfirmed(false); }} disabled={!subjectId || classes.isLoading} required>
            <option value="" disabled>{classes.isLoading ? "Loading classes..." : "Select a class"}</option>
            {(classes.data ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </SelectInput>
        </Field>
        {subjectId && classes.data?.length === 0 ? <StateBlock title="No classes available" /> : null}
        {subjects.error ? <FormMessage tone="error">{subjects.error.message}</FormMessage> : null}
        {classes.error ? <FormMessage tone="error">{classes.error.message}</FormMessage> : null}
        {saveEnrollment.error ? <FormMessage tone="error">{describeError(saveEnrollment.error)}</FormMessage> : null}
        {conflictSubjectId === subjectId && !conflictConfirmed ? (
          <Button type="button" variant="secondary" onClick={() => setConflictConfirmed(true)}>
            Use refreshed data and retry
          </Button>
        ) : null}
        {successMessage ? <FormMessage tone="success">{successMessage}</FormMessage> : null}
        <Button
          type="submit"
          disabled={!subjectId || !classId || saveEnrollment.isPending || (conflictSubjectId === subjectId && !conflictConfirmed)}
        >
          <Save aria-hidden="true" />{saveEnrollment.isPending ? "Saving..." : "Save enrollment"}
        </Button>
      </form>
    </section>
  );
}
