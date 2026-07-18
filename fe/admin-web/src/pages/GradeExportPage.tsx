import { useState } from "react";
import { Download } from "lucide-react";
import { utils, writeFile } from "xlsx";
import { Button } from "../components/ui/Button";
import { Field, SelectInput, TextInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { useGradeTable, type GradeTableRow } from "../hooks/useGradeTable";
import { useSubjects, useAssignments } from "../hooks/useSubjects";
import { useClasses } from "../hooks/useClasses";
import { ApiError } from "../lib/apiClient";
import { sanitizeSpreadsheetCell } from "../lib/validation";

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status >= 500) {
      return "Something went wrong loading grades. Please try again.";
    }
    return error.message || "Request failed. Please try again.";
  }
  return "Connection lost. Please check your internet and retry.";
}

function matchesFilter(value: string | null, query: string): boolean {
  return (value ?? "").toLowerCase().includes(query.trim().toLowerCase());
}

function buildExportFilename(assignmentTitle: string): string {
  const slug = assignmentTitle.trim().toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
  const date = new Date().toISOString().slice(0, 10);
  return `${slug || "assignment"}-grades-${date}.xlsx`;
}

function exportToExcel(rows: GradeTableRow[], assignmentTitle: string) {
  const sheetRows = rows.map((row) => ({
    "Student Name": sanitizeSpreadsheetCell(row.studentName),
    MSSV: sanitizeSpreadsheetCell(row.mssv ?? ""),
    "Class Name": sanitizeSpreadsheetCell(row.className ?? ""),
    "Final Score": row.finalScore ?? "",
  }));

  const worksheet = utils.json_to_sheet(sheetRows);
  const workbook = utils.book_new();
  utils.book_append_sheet(workbook, worksheet, "Grades");
  writeFile(workbook, buildExportFilename(assignmentTitle));
}

export function GradeExportPage() {
  const [assignmentId, setAssignmentId] = useState("");
  const [mssvFilter, setMssvFilter] = useState("");
  const [classFilter, setClassFilter] = useState("");
  const [subjectId, setSubjectId] = useState("");

  const subjects = useSubjects({ pageSize: 1000 });
  const assignments = useAssignments(subjectId, { pageSize: 1000 });
  const classes = useClasses();
  
  const gradeTable = useGradeTable(assignmentId || undefined);

  const selectedAssignment = (assignments.data?.items ?? []).find((assignment) => assignment.id === assignmentId) ?? null;

  const filteredRows = (gradeTable.data ?? []).filter(
    (row) => matchesFilter(row.mssv, mssvFilter) && matchesFilter(row.className, classFilter),
  );

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Grades</p>
        <h1>Grade table & export</h1>
      </header>
      <div className="form-panel">
        <Field label="Subject">
          <SelectInput value={subjectId} onChange={(event) => { setSubjectId(event.target.value); setAssignmentId(""); }}>
            <option value="">All subjects</option>
            {(subjects.data?.items ?? []).map((subject) => (
              <option key={subject.id} value={subject.id}>
                {subject.code} - {subject.name}
              </option>
            ))}
          </SelectInput>
        </Field>
        <Field label="Assignment">
          <SelectInput value={assignmentId} onChange={(event) => setAssignmentId(event.target.value)}>
            <option value="">Select an assignment</option>
            {(assignments.data?.items ?? []).map((assignment) => (
              <option key={assignment.id} value={assignment.id}>
                {assignment.title}
              </option>
            ))}
          </SelectInput>
        </Field>
        {assignments.error ? <FormMessage tone="error">{describeError(assignments.error)}</FormMessage> : null}
      </div>

      {!assignmentId ? <StateBlock title="Select an assignment to view grades" /> : null}

      {assignmentId ? (
        <div className="table-panel">
          <div className="filter-bar">
            <Field label="Filter by MSSV">
              <TextInput value={mssvFilter} onChange={(event) => setMssvFilter(event.target.value)} placeholder="SE123456" />
            </Field>
            <Field label="Filter by class">
              <SelectInput value={classFilter} onChange={(event) => setClassFilter(event.target.value)}>
                <option value="">All classes</option>
                {(classes.data ?? []).map((klass) => (
                  <option key={klass.id} value={klass.name}>
                    {klass.name}
                  </option>
                ))}
              </SelectInput>
            </Field>
          </div>
          <p>Showing rows where MSSV contains the filter AND Class contains the filter.</p>

          {gradeTable.isLoading ? <StateBlock title="Loading grades" /> : null}
          {gradeTable.error ? (
            <>
              <FormMessage tone="error">{describeError(gradeTable.error)}</FormMessage>
              <Button type="button" variant="secondary" onClick={() => gradeTable.refetch()}>
                Retry
              </Button>
            </>
          ) : null}
          {gradeTable.data && gradeTable.data.length === 0 ? (
            <StateBlock title="No submissions for this assignment" />
          ) : null}
          {gradeTable.data && gradeTable.data.length > 0 && filteredRows.length === 0 ? (
            <StateBlock title="No results match the current filters" />
          ) : null}

          {filteredRows.length > 0 ? (
            <>
              <Button
                type="button"
                variant="secondary"
                onClick={() => exportToExcel(filteredRows, selectedAssignment?.title ?? "assignment")}
              >
                <Download aria-hidden="true" />
                Export to Excel
              </Button>
              <table>
                <thead>
                  <tr>
                    <th>Student Name</th>
                    <th>MSSV</th>
                    <th>Class Name</th>
                    <th>Final Score</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.map((row) => (
                    <tr key={row.submissionId}>
                      <td>{row.studentName}</td>
                      <td>{row.mssv || "-"}</td>
                      <td>{row.className || "-"}</td>
                      <td>{row.finalScore ?? "Not graded"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
