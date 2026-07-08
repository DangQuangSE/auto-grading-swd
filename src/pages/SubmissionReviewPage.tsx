import { Save, Send } from "lucide-react";
import { StatusBadge } from "../components/StatusBadge";

const criteria = [
  {
    code: "C1",
    title: "Architecture consistency",
    max: 4,
    suggested: 3,
    evidence: "Diagram contains API Gateway, Auth Service, Postgres, and Storage.",
    comment: "Core components are present, but service boundaries need clearer justification.",
  },
  {
    code: "C2",
    title: "Requirement coverage",
    max: 6,
    suggested: 5,
    evidence: "Document sections 2.1 and 2.3 describe upload and lecturer review workflow.",
    comment: "Workflow is mostly complete; failure retry behavior is light.",
  },
];

export function SubmissionReviewPage() {
  return (
    <section className="page-grid">
      <header className="page-header row-header">
        <div>
          <p>Review</p>
          <h1>SE170001 - SWD final project</h1>
        </div>
        <StatusBadge state="graded" />
      </header>
      <div className="split-review">
        <section className="evidence-panel">
          <h2>Extracted evidence</h2>
          <article>
            <h3>Document sections</h3>
            <p>1. System Overview</p>
            <p>2. Architecture Design</p>
            <p>3. Error Handling</p>
          </article>
          <article>
            <h3>Diagram entities</h3>
            <p>React Web, Supabase Auth, Storage, Edge Function, Postgres, OpenRouter</p>
          </article>
        </section>
        <section className="grading-panel">
          <h2>Criterion scores</h2>
          {criteria.map((criterion) => (
            <article className="criterion-row" key={criterion.code}>
              <div>
                <span>{criterion.code}</span>
                <h3>{criterion.title}</h3>
                <p>{criterion.evidence}</p>
                <p>{criterion.comment}</p>
              </div>
              <label>
                Final
                <input
                  type="number"
                  min="0"
                  max={criterion.max}
                  step="0.25"
                  defaultValue={criterion.suggested}
                />
                <small>AI: {criterion.suggested} / {criterion.max}</small>
              </label>
            </article>
          ))}
          <div className="button-row">
            <button className="secondary-button" type="button">
              <Save aria-hidden="true" />
              Save
            </button>
            <button className="primary-button" type="button">
              <Send aria-hidden="true" />
              Publish
            </button>
          </div>
        </section>
      </div>
    </section>
  );
}
