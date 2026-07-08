import { AlertTriangle, CheckCircle2, Clock3 } from "lucide-react";
import { StatusBadge } from "../components/StatusBadge";

const rows = [
  { student: "SE170001", subject: "SWD", state: "graded" as const, score: "8.0 / 10" },
  { student: "SE170014", subject: "SWR", state: "extracting" as const, score: "-" },
  { student: "SE170088", subject: "SWD", state: "failed" as const, score: "-" },
];

export function LecturerDashboard() {
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
          <strong>12</strong>
        </article>
        <article className="metric-panel">
          <Clock3 aria-hidden="true" />
          <span>Processing</span>
          <strong>4</strong>
        </article>
        <article className="metric-panel">
          <AlertTriangle aria-hidden="true" />
          <span>Needs action</span>
          <strong>2</strong>
        </article>
      </div>
      <div className="table-panel">
        <table>
          <thead>
            <tr>
              <th>Student</th>
              <th>Subject</th>
              <th>Status</th>
              <th>AI score</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.student}>
                <td>{row.student}</td>
                <td>{row.subject}</td>
                <td>
                  <StatusBadge state={row.state} />
                </td>
                <td>{row.score}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
