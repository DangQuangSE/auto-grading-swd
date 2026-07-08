import { CheckCircle2 } from "lucide-react";

export function StudentResultPage() {
  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Result</p>
        <h1>Published feedback</h1>
      </header>
      <div className="result-panel">
        <CheckCircle2 aria-hidden="true" />
        <strong>8.0 / 10</strong>
        <p>
          Architecture is coherent and most requirements are covered. Improve retry behavior and
          clarify how grading failures are surfaced to lecturers.
        </p>
      </div>
    </section>
  );
}
