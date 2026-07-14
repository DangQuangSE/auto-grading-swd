import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { StateBlock } from "../components/ui/StateBlock";
import { getGradingRuns, getFinalGrade } from "../services/gradingService";
import { listMySubmissions } from "../services/submissionService";
import { useAuth } from "../providers/AuthProvider";
import { useState } from "react";

export function StudentResultPage() {
  const { submissionId: paramId } = useParams<{ submissionId?: string }>();
  const { session } = useAuth();
  const [selectedId, setSelectedId] = useState(paramId ?? "");

  const submissions = useQuery({
    queryKey: ["my-submissions", session?.user.id],
    queryFn: () => listMySubmissions(session!.user.id),
    enabled: Boolean(session),
  });

  const runs = useQuery({
    queryKey: ["grading-runs", selectedId],
    queryFn: () => getGradingRuns(selectedId),
    enabled: Boolean(selectedId),
    // Poll every 3 s while the latest run is still running
    refetchInterval: (query) => {
      const latest = query.state.data?.[0];
      return latest?.status === "running" ? 3000 : false;
    },
  });

  const grade = useQuery({
    queryKey: ["final-grade", selectedId],
    queryFn: () => getFinalGrade(selectedId),
    enabled: Boolean(selectedId),
    refetchInterval: (query) => (query.state.data ? false : 5000),
  });

  const latestRun = runs.data?.[0] ?? null;
  const isRunning = latestRun?.status === "running";
  const totalMax = latestRun?.scores.reduce((s, c) => s + c.maxScore, 0) ?? 0;
  const totalSuggested = latestRun?.scores.reduce((s, c) => s + c.suggestedScore, 0) ?? 0;

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Result</p>
        <h1>AI grading result</h1>
      </header>

      {(submissions.data ?? []).length > 0 && (
        <div className="form-panel">
          <label htmlFor="sub-select" style={{ fontWeight: 600 }}>Submission</label>
          <select
            id="sub-select"
            value={selectedId}
            onChange={(e) => setSelectedId(e.target.value)}
            style={{ width: "100%", padding: "0.5rem", borderRadius: "0.5rem", border: "1px solid #ccc" }}
          >
            <option value="">Select a submission</option>
            {(submissions.data ?? []).map((s) => (
              <option key={s.id} value={s.id}>
                {s.id.slice(0, 8)} — {s.state} — {new Date(s.createdAt).toLocaleString()}
              </option>
            ))}
          </select>
        </div>
      )}

      {!selectedId && (
        <StateBlock title="No submission selected" detail="Select a submission above to view results." />
      )}

      {selectedId && runs.isLoading && <StateBlock title="Loading results…" />}

      {runs.error && <StateBlock title="Error" detail={(runs.error as Error).message} />}

      {selectedId && !runs.isLoading && !latestRun && (
        <StateBlock title="Grading not started" detail="The AI grading job has not run yet. Please wait." />
      )}

      {isRunning && (
        <div style={{ padding: "1rem", background: "#f0f9ff", borderRadius: "0.5rem", marginBottom: "1rem", color: "#0369a1" }}>
          ⏳ AI is grading your submission… refreshing automatically every 3 s.
        </div>
      )}

      {latestRun && !isRunning && (
        <div className="result-panel">
          {grade.data ? (
            <p style={{ fontSize: "0.85rem", color: "green", fontWeight: 600 }}>
              ✓ Final grade published: {grade.data.finalScore}
            </p>
          ) : (
            <p style={{ fontSize: "0.85rem", color: "#888" }}>AI suggested — pending lecturer review</p>
          )}

          <strong style={{ fontSize: "1.5rem" }}>
            {latestRun.status === "failed"
              ? "Grading failed"
              : `${totalSuggested.toFixed(1)} / ${totalMax}`}
          </strong>

          {latestRun.status === "failed" && (
            <p style={{ color: "#dc2626", fontSize: "0.875rem" }}>
              The grading job failed. Ask your lecturer to re-grade once a rubric is uploaded.
            </p>
          )}

          <p style={{ fontSize: "0.8rem", color: "#666" }}>
            Model: {latestRun.model} · {latestRun.completedAt ? new Date(latestRun.completedAt).toLocaleString() : "—"}
          </p>

          {latestRun.scores.length > 0 && (
            <table style={{ width: "100%", marginTop: "1rem", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ textAlign: "left", borderBottom: "1px solid #eee" }}>
                  <th style={{ padding: "0.4rem" }}>Criterion</th>
                  <th style={{ padding: "0.4rem" }}>Score</th>
                  <th style={{ padding: "0.4rem" }}>Evidence</th>
                  <th style={{ padding: "0.4rem" }}>Comment</th>
                </tr>
              </thead>
              <tbody>
                {latestRun.scores.map((score) => (
                  <tr key={score.id} style={{ borderBottom: "1px solid #f5f5f5" }}>
                    <td style={{ padding: "0.4rem", fontFamily: "monospace", fontSize: "0.75rem" }}>
                      {score.rubricCriterionId.slice(0, 8)}
                    </td>
                    <td style={{ padding: "0.4rem", fontWeight: 600 }}>
                      {score.suggestedScore} / {score.maxScore}
                    </td>
                    <td style={{ padding: "0.4rem", color: "#555" }}>{score.evidence ?? "—"}</td>
                    <td style={{ padding: "0.4rem", color: "#555" }}>{score.comment ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {grade.data?.notes && (
            <p style={{ marginTop: "1rem", fontStyle: "italic" }}>Lecturer note: {grade.data.notes}</p>
          )}
        </div>
      )}
    </section>
  );
}
