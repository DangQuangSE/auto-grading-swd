import { useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "../components/ui/Button";
import { Field, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { Pagination } from "../components/ui/Pagination";
import { StateBlock } from "../components/ui/StateBlock";
import { useCreateSubject, useSubjects, useUpdateSubjectRegistration } from "../hooks/useSubjects";
import { DEFAULT_PAGE, DEFAULT_PAGE_SIZE } from "../lib/pagination";
import { useAuth } from "../providers/AuthProvider";

export function SubjectsPage() {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [page, setPage] = useState(DEFAULT_PAGE);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [registrationMessage, setRegistrationMessage] = useState("");
  const { session } = useAuth();
  const subjects = useSubjects({ page, pageSize });
  const createSubject = useCreateSubject();
  const updateRegistration = useUpdateSubjectRegistration();
  const canManageRegistration = session?.user.role === "admin";

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!code.trim() || !name.trim() || !session) {
      return;
    }

    try {
      await createSubject.mutateAsync({
        code: code.trim(),
        name: name.trim(),
        createdBy: session.user.id,
      });
      setCode("");
      setName("");
      setPage(DEFAULT_PAGE);
    } catch {
      // Mutation state renders the error.
    }
  }

  async function handleRegistrationChange(subjectId: string, status: "open" | "closed") {
    setRegistrationMessage("");
    try {
      await updateRegistration.mutateAsync({ subjectId, status });
      setRegistrationMessage(`Registration is now ${status}.`);
    } catch {
      // Mutation state renders the error.
    }
  }

  function handlePageSizeChange(nextPageSize: number) {
    setPageSize(nextPageSize);
    setPage(DEFAULT_PAGE);
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
        {subjects.data && subjects.data.items.length === 0 ? (
          <StateBlock title="No subjects yet" detail="Create a subject to start uploading rubrics and assignments." />
        ) : null}
        {subjects.data && subjects.data.items.length > 0 ? (
          <>
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Registration</th>
                  <th>Created</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {subjects.data.items.map((subject) => (
                  <tr key={subject.id}>
                    <td>{subject.code}</td>
                    <td>{subject.name}</td>
                    <td>{subject.registrationStatus === "open" ? "Open" : "Closed"}</td>
                    <td>{new Date(subject.createdAt).toLocaleString()}</td>
                    <td>
                      {canManageRegistration ? (
                        <Button
                          type="button"
                          variant="text"
                          disabled={updateRegistration.isPending}
                          onClick={() => void handleRegistrationChange(
                            subject.id,
                            subject.registrationStatus === "open" ? "closed" : "open",
                          )}
                        >
                          {subject.registrationStatus === "open" ? "Close registration" : "Open registration"}
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination
              page={subjects.data.page}
              pageSize={subjects.data.pageSize}
              totalCount={subjects.data.totalCount}
              totalPages={subjects.data.totalPages}
              onPageChange={setPage}
              onPageSizeChange={handlePageSizeChange}
            />
          </>
        ) : null}
        {updateRegistration.error ? <FormMessage tone="error">{updateRegistration.error.message}</FormMessage> : null}
        {registrationMessage ? <FormMessage tone="success">{registrationMessage}</FormMessage> : null}
      </div>
    </section>
  );
}
