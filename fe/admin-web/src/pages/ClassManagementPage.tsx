import { useState } from "react";
import { Plus, Users } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "../components/ui/Button";
import { Field, SelectInput, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { useClasses, useCreateClass, useLecturers, useUpdateClassLecturer } from "../hooks/useClasses";

function lecturerLabel(lecturer: { fullName: string; email: string } | undefined, lecturerId: string | null) {
  if (!lecturerId) {
    return "Unassigned";
  }
  return lecturer ? `${lecturer.fullName} (${lecturer.email})` : lecturerId;
}

export function ClassManagementPage() {
  const [name, setName] = useState("");
  const [lecturerId, setLecturerId] = useState("");
  const [editingClassId, setEditingClassId] = useState<string | null>(null);
  const [reassignLecturerId, setReassignLecturerId] = useState("");

  const classes = useClasses();
  const lecturers = useLecturers();
  const createClass = useCreateClass();
  const updateLecturer = useUpdateClassLecturer();

  const lecturersById = new Map((lecturers.data ?? []).map((lecturer) => [lecturer.id, lecturer]));

  async function handleCreateSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!name.trim() || !lecturerId) {
      return;
    }

    try {
      await createClass.mutateAsync({ name: name.trim(), lecturerId });
      setName("");
      setLecturerId("");
    } catch {
      // surfaced via createClass.error below
    }
  }

  function startReassign(classId: string, currentLecturerId: string | null) {
    setEditingClassId(classId);
    setReassignLecturerId(currentLecturerId ?? "");
  }

  function cancelReassign() {
    setEditingClassId(null);
    setReassignLecturerId("");
  }

  async function handleReassignSubmit(classId: string) {
    if (!reassignLecturerId) {
      return;
    }

    try {
      await updateLecturer.mutateAsync({ classId, lecturerId: reassignLecturerId });
      setEditingClassId(null);
      setReassignLecturerId("");
    } catch {
      // surfaced via updateLecturer.error below
    }
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Classes</p>
        <h1>Manage classes</h1>
        <Link to="/roster" className="secondary-button">
          <Users aria-hidden="true" />
          Student roster
        </Link>
      </header>
      <form className="form-panel" onSubmit={handleCreateSubmit}>
        <Field label="Class name">
          <TextInput value={name} onChange={(event) => setName(event.target.value)} placeholder="SE1801" required />
        </Field>
        <Field label="Lecturer">
          <SelectInput value={lecturerId} onChange={(event) => setLecturerId(event.target.value)} required>
            <option value="" disabled>
              Select a lecturer
            </option>
            {(lecturers.data ?? []).map((lecturer) => (
              <option key={lecturer.id} value={lecturer.id}>
                {lecturer.fullName} ({lecturer.email})
              </option>
            ))}
          </SelectInput>
        </Field>
        {createClass.error ? <FormMessage tone="error">{createClass.error.message}</FormMessage> : null}
        <Button type="submit" disabled={!name.trim() || !lecturerId || createClass.isPending}>
          <Plus aria-hidden="true" />
          {createClass.isPending ? "Creating..." : "Create class"}
        </Button>
      </form>
      <div className="table-panel">
        {classes.isLoading ? <StateBlock title="Loading classes" /> : null}
        {classes.error ? <FormMessage tone="error">{classes.error.message}</FormMessage> : null}
        {classes.data && classes.data.length === 0 ? (
          <StateBlock title="No classes yet" detail="Create a class and assign a lecturer to get started." />
        ) : null}
        {classes.data && classes.data.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Lecturer</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {classes.data.map((klass) => (
                <tr key={klass.id}>
                  <td>{klass.name}</td>
                  <td>
                    {editingClassId === klass.id ? (
                      <SelectInput
                        value={reassignLecturerId}
                        onChange={(event) => setReassignLecturerId(event.target.value)}
                        disabled={updateLecturer.isPending}
                      >
                        <option value="" disabled>
                          Select a lecturer
                        </option>
                        {(lecturers.data ?? []).map((lecturer) => (
                          <option key={lecturer.id} value={lecturer.id}>
                            {lecturer.fullName} ({lecturer.email})
                          </option>
                        ))}
                      </SelectInput>
                    ) : (
                      lecturerLabel(klass.lecturerId ? lecturersById.get(klass.lecturerId) : undefined, klass.lecturerId)
                    )}
                  </td>
                  <td>
                    {editingClassId === klass.id ? (
                      <>
                        <Button
                          type="button"
                          variant="secondary"
                          disabled={!reassignLecturerId || updateLecturer.isPending}
                          onClick={() => handleReassignSubmit(klass.id)}
                        >
                          {updateLecturer.isPending ? "Saving..." : "Save"}
                        </Button>
                        <Button type="button" variant="text" onClick={cancelReassign} disabled={updateLecturer.isPending}>
                          Cancel
                        </Button>
                      </>
                    ) : (
                      <Button type="button" variant="text" onClick={() => startReassign(klass.id, klass.lecturerId)}>
                        Reassign
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
        {updateLecturer.error ? <FormMessage tone="error">{updateLecturer.error.message}</FormMessage> : null}
      </div>
    </section>
  );
}
