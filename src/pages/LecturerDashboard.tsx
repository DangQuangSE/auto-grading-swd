import { AlertTriangle, CheckCircle2, Clock3 } from "lucide-react";
import { StatusBadge } from "../components/StatusBadge";
import { StateBlock } from "../components/ui/StateBlock";
import { useRecentSubmissions } from "../hooks/useSubmissions";

export function LecturerDashboard() {
  const submissions = useRecentSubmissions();
  const rows = submissions.data ?? [];
  const readyCount = rows.filter((row) => row.state === "graded" || row.state === "reviewed").length;
  const processingCount = rows.filter((row) => row.state === "extracting" || row.state === "grading").length;
  const failedCount = rows.filter((row) => row.state === "failed").length;

  return (
    <section className="page-grid">
      <header className="page-header">
        <p>Lecturer</p>
        <h1>Submission dashboard</h1>
      </header>
      <div className="metric-grid">
        <article className="metric-panel">
          <CheckCircle2 aria-hidden="true" />
          <span>Ready to review</span>
          <strong>{readyCount}</strong>
        </article>
        <article className="metric-panel">
          <Clock3 aria-hidden="true" />
          <span>Processing</span>
          <strong>{processingCount}</strong>
        </article>
        <article className="metric-panel">
          <AlertTriangle aria-hidden="true" />
          <span>Needs action</span>
          <strong>{failedCount}</strong>
        </article>
      </div>
      <div className="table-panel">
        {submissions.isLoading ? <StateBlock title="Loading submissions" /> : null}
        {submissions.error ? <StateBlock title="Unable to load submissions" detail={submissions.error.message} /> : null}
        {!submissions.isLoading && rows.length === 0 ? (
          <StateBlock title="No submissions yet" detail="Student uploads will appear here after the first submission." />
        ) : null}
        {rows.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Student</th>
                <th>Assignment</th>
                <th>Status</th>
                <th>Submitted</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  <td>{row.student_id.slice(0, 8)}</td>
                  <td>{row.assignment_id.slice(0, 8)}</td>
                  <td>
                    <StatusBadge state={row.state} />
                  </td>
                  <td>{new Date(row.submitted_at).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>
    </section>
  );
}
