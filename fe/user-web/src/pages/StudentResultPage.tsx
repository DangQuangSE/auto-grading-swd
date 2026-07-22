import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { StateBlock } from "../components/ui/StateBlock";
import { getGradingRuns, getFinalGrade } from "../services/gradingService";
import { listMySubmissions } from "../services/submissionService";
import { useAuth } from "../providers/AuthProvider";
import { useState, useEffect } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { ProgressBar, GradingStatus } from "../components/ProgressBar";
import { Button } from "../components/ui/Button";
import { apiPost, apiGet } from "../lib/apiClient";
import { useSubjects, useAllAssignments } from "../hooks/useSubjects";

function getFileName(objectKey: string | undefined): string {
  if (!objectKey) return "Unknown file";
  const filePart = objectKey.split("/").pop() || "";
  if (/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}-/.test(filePart)) {
    return filePart.substring(37);
  }
  return filePart;
}

export function StudentResultPage() {
  const { submissionId: paramId } = useParams<{ submissionId?: string }>();
  const { session } = useAuth();
  const [selectedId, setSelectedId] = useState(paramId ?? "");
  const [liveStatus, setLiveStatus] = useState<GradingStatus>(null);
  const [liveError, setLiveError] = useState<string | null>(null);

  // Update selectedId whenever paramId URL param changes
  useEffect(() => {
    if (paramId) {
      setSelectedId(paramId);
    }
  }, [paramId]);

  const queryClient = useQueryClient();

  const runs = useQuery({
    queryKey: ["grading-runs", selectedId],
    queryFn: () => getGradingRuns(selectedId),
    enabled: Boolean(selectedId),
    refetchInterval: (query) => {
      const runStatus = query.state.data?.[0]?.status;
      if (runStatus === "completed" || runStatus === "failed") return false;
      return selectedId ? 2000 : false;
    },
  });

  const submissions = useQuery({
    queryKey: ["my-submissions", session?.user.id],
    queryFn: () => listMySubmissions(session!.user.id),
    enabled: Boolean(session),
    refetchInterval: (query) => {
      const currentSub = query.state.data?.find(s => s.id === selectedId);
      const stateStr = currentSub?.state?.toLowerCase();
      if (stateStr === "failed") return false;
      const latestRunStatus = runs.data?.[0]?.status;
      if (latestRunStatus === "completed" || latestRunStatus === "failed") return false;
      return selectedId ? 2000 : false;
    },
  });

  const latestRun = runs.data?.[0] ?? null;
  const sub = submissions.data?.find(s => s.id === selectedId);

  let baseStatus: GradingStatus = null;
  if (sub) {
     const stateStr = String(sub.state).toLowerCase();
     if (stateStr === "uploaded") baseStatus = "Uploaded";
     if (stateStr === "extracting") baseStatus = "Extracting";
     if (stateStr === "extracted") baseStatus = "AiGrading";
     if (stateStr === "failed") baseStatus = "ExtractionFailed";
  }

  if (latestRun) {
    if (latestRun.status === "running") baseStatus = "AiGrading";
    if (latestRun.status === "completed") baseStatus = "Completed";
    if (latestRun.status === "failed") baseStatus = "AiGradingFailed";
  }

  const isFinished = baseStatus === "Completed" || baseStatus === "AiGradingFailed" || baseStatus === "ExtractionFailed";
  const effectiveStatus = isFinished ? baseStatus : (liveStatus ?? baseStatus);

  // Auto-select latest submission if no submission ID selected or if paramId is absent
  useEffect(() => {
    if (!paramId && !selectedId && submissions.data && submissions.data.length > 0) {
      setSelectedId(submissions.data[0].id);
    }
  }, [paramId, selectedId, submissions.data]);

  const subjects = useSubjects();
  const assignments = useAllAssignments();

  const retryMutation = useMutation({
    mutationFn: () => apiPost(`/submissions/submissions/${selectedId}/retry`),
    onSuccess: () => {
      setLiveStatus("Uploaded");
      setLiveError(null);
      queryClient.invalidateQueries({ queryKey: ["my-submissions"] });
      queryClient.invalidateQueries({ queryKey: ["grading-runs", selectedId] });
    },
  });

  // Clear liveStatus when changing submission
  useEffect(() => {
    setLiveStatus(null);
    setLiveError(null);
  }, [selectedId]);

  useEffect(() => {
    if (!session?.token) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_BASE_URL}/notifications/hub?access_token=${session.token}`)
      .withAutomaticReconnect()
      .build();

    connection.on("SubmissionUpdated", (data: any) => {
      queryClient.invalidateQueries({ queryKey: ["my-submissions"] });

      if (data.submissionId === selectedId) {
        setLiveStatus(data.status);
        if (data.errorMessage) {
          setLiveError(data.errorMessage);
        }

        queryClient.invalidateQueries({ queryKey: ["grading-runs", selectedId] });
        if (data.status === "Completed") {
          queryClient.invalidateQueries({ queryKey: ["final-grade", selectedId] });
        }
      }
    });

    connection.start().catch(console.error);

    return () => {
      connection.stop();
    };
  }, [session?.token, selectedId, queryClient]);

  const grade = useQuery({
    queryKey: ["final-grade", selectedId],
    queryFn: () => getFinalGrade(selectedId),
    enabled: Boolean(selectedId),
    refetchInterval: (query) => (query.state.data ? false : 5000),
  });

  const rubricQuery = useQuery({
    queryKey: ["rubric-for-assignment", sub?.assignmentId],
    queryFn: async () => {
      if (!sub?.assignmentId) return null;
      const rubrics = await apiGet<any[]>(`/catalog/rubrics?assignmentId=${sub.assignmentId}`);
      return rubrics?.[0] ?? null;
    },
    enabled: Boolean(sub?.assignmentId),
  });


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
            {(submissions.data ?? []).map((s) => {
              const assignment = assignments.data?.find(a => a.id === s.assignmentId);
              const subject = subjects.data?.find(sub => sub.id === assignment?.subjectId);
              const subjectName = subject?.code || "Unknown Subject";
              const assignmentName = assignment?.title || "Unknown Assignment";
              const fileName = getFileName(s.reportObjectKey);
              const time = new Date(s.createdAt).toLocaleString();
              
              return (
                <option key={s.id} value={s.id}>
                  {subjectName} - {assignmentName} - {fileName} - {time}
                </option>
              );
            })}
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
          ⏳ AI is grading your submission…
        </div>
      )}

      {selectedId && effectiveStatus && (
        <div style={{ maxWidth: "800px", margin: "0 auto" }}>
          <ProgressBar status={effectiveStatus} />
        </div>
      )}
      {liveError && <StateBlock title="Processing Error" detail={liveError} />}
      {(effectiveStatus === "ExtractionFailed" || effectiveStatus === "AiGradingFailed") && (
        <div style={{ marginBottom: "2rem" }}>
           <Button onClick={() => retryMutation.mutate()} disabled={retryMutation.isPending}>
             {retryMutation.isPending ? "Retrying..." : "Thử chấm lại"}
           </Button>
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
            <div style={{ overflowX: "auto", width: "100%", marginTop: "1rem" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem", minWidth: "600px" }}>
                <thead>
                  <tr style={{ textAlign: "left", borderBottom: "1px solid #eee" }}>
                    <th style={{ padding: "0.4rem" }}>Criterion</th>
                    <th style={{ padding: "0.4rem" }}>Score</th>
                    <th style={{ padding: "0.4rem" }}>Evidence</th>
                    <th style={{ padding: "0.4rem" }}>Comment</th>
                  </tr>
                </thead>
                <tbody>
                  {latestRun.scores.map((score) => {
                    const criterion = rubricQuery.data?.criteria?.find((c: any) => c.id === score.rubricCriterionId);
                    const criterionName = criterion?.name || score.rubricCriterionId.slice(0, 8);
                    return (
                      <tr key={score.id} style={{ borderBottom: "1px solid #f5f5f5" }}>
                        <td style={{ padding: "0.4rem", fontWeight: 500, whiteSpace: "nowrap" }}>
                          {criterionName}
                        </td>
                        <td style={{ padding: "0.4rem", fontWeight: 600, whiteSpace: "nowrap" }}>
                          {score.suggestedScore} / {score.maxScore}
                        </td>
                        <td style={{ padding: "0.4rem", color: "#555" }}>{score.evidence ?? "—"}</td>
                        <td style={{ padding: "0.4rem", color: "#555" }}>{score.comment ?? "—"}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {grade.data?.notes && (
            <p style={{ marginTop: "1rem", fontStyle: "italic" }}>Lecturer note: {grade.data.notes}</p>
          )}
        </div>
      )}
    </section>
  );
}
