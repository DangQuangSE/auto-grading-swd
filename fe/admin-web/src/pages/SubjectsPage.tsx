import { useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "../components/ui/Button";
import { Field, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { useCreateSubject, useSubjects } from "../hooks/useSubjects";
import { useAuth } from "../providers/AuthProvider";

export function SubjectsPage() {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const { session } = useAuth();
  const subjects = useSubjects();
  const createSubject = useCreateSubject();

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!code.trim() || !name.trim() || !session) {
      return;
    }

    await createSubject.mutateAsync({
      code: code.trim(),
      name: name.trim(),
      createdBy: session.user.id,
    });
    setCode("");
    setName("");
  }

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Subjects</p>
        <h1>Manage subjects</h1>
      </header>
      <form className="form-panel" onSubmit={handleSubmit}>
        <Field label="Subject code">
          <TextInput value={code} onChange={(event) => setCode(event.target.value)} placeholder="SWD392" required />
        </Field>
        <Field label="Subject name">
          <TextInput
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Software Development"
            required
          />
        </Field>
        {createSubject.error ? <FormMessage tone="error">{createSubject.error.message}</FormMessage> : null}
        <Button type="submit" disabled={!code.trim() || !name.trim() || createSubject.isPending}>
          <Plus aria-hidden="true" />
          {createSubject.isPending ? "Creating..." : "Create subject"}
        </Button>
      </form>
      <div className="table-panel">
        {subjects.isLoading ? <StateBlock title="Loading subjects" /> : null}
        {subjects.error ? <FormMessage tone="error">{subjects.error.message}</FormMessage> : null}
        {(subjects.data ?? []).length === 0 && !subjects.isLoading ? (
          <StateBlock title="No subjects yet" detail="Create a subject to start uploading rubrics and assignments." />
        ) : null}
        {(subjects.data ?? []).length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {(subjects.data ?? []).map((subject) => (
                <tr key={subject.id}>
                  <td>{subject.code}</td>
                  <td>{subject.name}</td>
                  <td>{new Date(subject.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>
    </section>
  );
}
