import { RefreshCw, Save, Send } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { StatusBadge } from "../components/StatusBadge";
import { Button } from "../components/ui/Button";
import { Field, TextInput, SelectInput } from "../components/ui/Field";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { useClasses } from "../hooks/useClasses";
import { useGradeTable } from "../hooks/useGradeTable";
import { useSubjects, useAssignments } from "../hooks/useSubjects";
import { usePublishGrade, useRecentSubmissions, useRegrade, useSaveFinalScore, useSubmissionReview } from "../hooks/useSubmissions";
import { useAuth } from "../providers/AuthProvider";

function matchesFilter(value: string | null, query: string): boolean {
  return (value ?? "").toLowerCase().includes(query.trim().toLowerCase());
}

type ReviewScore = {
  id: string;
  rubric_criterion_id: string;
  suggested_score: number;
  max_score: number;
  comment: string;
  evidence: unknown;
  rubric_criteria?: {
    criterion_code?: string;
    title?: string;
    description?: string;
  } | null;
};

type Artifact = {
  artifact_type: "rubric" | "document" | "diagram";
  content: unknown;
  warnings: unknown;
};

function stringifyEvidence(value: unknown) {
  if (Array.isArray(value)) {
    return value
      .map((item) => {
        if (typeof item === "object" && item && "reference" in item) {
          return String((item as { reference?: unknown }).reference ?? "");
        }

        return String(item);
      })
      .filter(Boolean)
      .join(", ");
  }

  return "No evidence provided.";
}

export function SubmissionReviewPage() {
  const { submissionId } = useParams();
  const { session } = useAuth();
  const recentSubmissions = useRecentSubmissions();
  const review = useSubmissionReview(submissionId);
  const saveFinalScore = useSaveFinalScore();
  const publishGrade = usePublishGrade();
  const regrade = useRegrade();
  const [finalScores, setFinalScores] = useState<Record<string, number>>({});
  const [assignmentDescription, setAssignmentDescription] = useState("");
  const [subjectId, setSubjectId] = useState("");
  const [assignmentId, setAssignmentId] = useState("");
  const [mssvFilter, setMssvFilter] = useState("");
  const [classFilter, setClassFilter] = useState("");

  const subjects = useSubjects({ pageSize: 1000 });
  const assignments = useAssignments(subjectId, { pageSize: 1000 });
  const classes = useClasses();
  
  const gradeTable = useGradeTable(assignmentId || undefined);

  const aiScores = useMemo(
    () => ((review.data?.aiScores ?? []) as unknown as ReviewScore[]),
    [review.data?.aiScores],
  );
  const artifacts = (review.data?.artifacts ?? []) as unknown as Artifact[];

  useEffect(() => {
    setFinalScores(
      Object.fromEntries(aiScores.map((score) => [score.rubric_criterion_id, Number(score.suggested_score)])),
    );
  }, [aiScores]);

  async function handleRegrade() {
    if (!submissionId) return;
    await regrade.mutateAsync({ submissionId, assignmentDescription: assignmentDescription || null });
  }

  async function handleSave() {
    if (!submissionId || !session) {
      return;
    }

    for (const score of aiScores) {
      await saveFinalScore.mutateAsync({
        submissionId,
        criterionId: score.rubric_criterion_id,
        aiCriterionScoreId: score.id,
        finalScore: finalScores[score.rubric_criterion_id] ?? score.suggested_score,
        finalComment: score.comment,
        maxScore: score.max_score,
        lecturerId: session.user.id,
      });
    }
  }

  async function handlePublish() {
    if (!submissionId || !session) {
      return;
    }

    await publishGrade.mutateAsync({ submissionId, lecturerId: session.user.id });
  }

  if (!submissionId) {
    const recentRows = recentSubmissions.data ?? [];
    const filteredRows = (gradeTable.data ?? []).filter(
      (row) => matchesFilter(row.mssv, mssvFilter) && matchesFilter(row.className, classFilter),
    );

    return (
      <section className="page-grid compact-page">
        <header className="page-header">
          <p>Review</p>
          <h1>Select a submission</h1>
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
        </div>

        {!assignmentId ? (
          <div className="table-panel">
            {recentSubmissions.isLoading ? <StateBlock title="Loading submissions" /> : null}
            {recentRows.length === 0 && !recentSubmissions.isLoading ? (
              <StateBlock title="No submissions to review" detail="Once students submit files, review links will appear here." />
            ) : null}
            {recentRows.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Submission</th>
                    <th>Status</th>
                    <th>Submitted</th>
                    <th>Open</th>
                  </tr>
                </thead>
                <tbody>
                  {recentRows.map((row) => (
                    <tr key={row.id}>
                      <td>{row.id.slice(0, 8)}</td>
                      <td>
                        <StatusBadge state={row.state as any} />
                      </td>
                      <td>{new Date(row.submitted_at).toLocaleString()}</td>
                      <td>
                        <Link to={`/review/${row.id}`}>Review</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
          </div>
        ) : (
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
            
            {gradeTable.isLoading ? <StateBlock title="Loading submissions" /> : null}
            {gradeTable.data && gradeTable.data.length === 0 ? (
              <StateBlock title="No submissions for this assignment" />
            ) : null}
            {gradeTable.data && gradeTable.data.length > 0 && filteredRows.length === 0 ? (
              <StateBlock title="No results match the current filters" />
            ) : null}

            {filteredRows.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Submission</th>
                    <th>Student Name</th>
                    <th>MSSV</th>
                    <th>Class</th>
                    <th>Status</th>
                    <th>Submitted</th>
                    <th>Open</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.map((row) => (
                    <tr key={row.submissionId}>
                      <td>{row.submissionId.slice(0, 8)}</td>
                      <td>{row.studentName}</td>
                      <td>{row.mssv || "-"}</td>
                      <td>{row.className || "-"}</td>
                      <td>
                        <StatusBadge state={row.state as any} />
                      </td>
                      <td>{new Date(row.submittedAt).toLocaleString()}</td>
                      <td>
                        <Link to={`/review/${row.submissionId}`}>Review</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : null}
          </div>
        )}
      </section>
    );
  }

  if (review.isLoading) {
    return <StateBlock title="Loading review" />;
  }

  if (review.error) {
    return <StateBlock title="Unable to load review" detail={review.error.message} />;
  }

  const documentArtifact = artifacts.find((artifact) => artifact.artifact_type === "document");
  const diagramArtifact = artifacts.find((artifact) => artifact.artifact_type === "diagram");

  return (
    <section className="page-grid">
      <header className="page-header row-header">
        <div>
          <p>Review</p>
          <h1>{submissionId.slice(0, 8)} submission</h1>
        </div>
        {review.data?.submission ? <StatusBadge state={review.data.submission.state} /> : null}
      </header>
      <div className="split-review">
        <section className="evidence-panel">
          <h2>Extracted evidence</h2>
          <article>
            <h3>Document</h3>
            <pre className="json-preview">{JSON.stringify(documentArtifact?.content ?? {}, null, 2)}</pre>
          </article>
          <article>
            <h3>Diagram</h3>
            <pre className="json-preview">{JSON.stringify(diagramArtifact?.content ?? {}, null, 2)}</pre>
          </article>
        </section>
        <section className="grading-panel">
          <h2>Criterion scores</h2>
          {aiScores.length === 0 ? (
            <StateBlock title="No AI scores yet" detail="Run extraction and AI grading before reviewing final scores." />
          ) : null}
          {aiScores.map((score) => (
            <article className="criterion-row" key={score.id}>
              <div>
                <span>{score.rubric_criteria?.criterion_code ?? score.rubric_criterion_id.slice(0, 8)}</span>
                <h3>{score.rubric_criteria?.title ?? "Criterion"}</h3>
                <p>{stringifyEvidence(score.evidence)}</p>
                <p>{score.comment}</p>
              </div>
              <label>
                Final
                <input
                  type="number"
                  min="0"
                  max={score.max_score}
                  step="0.25"
                  value={finalScores[score.rubric_criterion_id] ?? score.suggested_score}
                  onChange={(event) =>
                    setFinalScores((current) => ({
                      ...current,
                      [score.rubric_criterion_id]: Number(event.target.value),
                    }))
                  }
                />
                <small>
                  AI: {score.suggested_score} / {score.max_score}
                </small>
              </label>
            </article>
          ))}
          {saveFinalScore.error ? <FormMessage tone="error">{saveFinalScore.error.message}</FormMessage> : null}
          {publishGrade.error ? <FormMessage tone="error">{publishGrade.error.message}</FormMessage> : null}
          {saveFinalScore.isSuccess ? <FormMessage tone="success">Final scores saved.</FormMessage> : null}
          {publishGrade.isSuccess ? <FormMessage tone="success">Grade published.</FormMessage> : null}
          <hr />
          <h3>Regrade with assignment description (mã đề)</h3>
          <Field label="Assignment description">
            <TextInput
              type="text"
              value={assignmentDescription}
              onChange={(e) => setAssignmentDescription(e.target.value)}
              placeholder="Paste assignment brief / mã đề here to improve AI grading accuracy"
            />
          </Field>
          {regrade.error ? <FormMessage tone="error">{regrade.error.message}</FormMessage> : null}
          {regrade.isSuccess ? <FormMessage tone="success">Regrade queued — scores will refresh shortly.</FormMessage> : null}
          <div className="button-row">
            <Button variant="secondary" type="button" onClick={handleSave} disabled={aiScores.length === 0 || saveFinalScore.isPending}>
              <Save aria-hidden="true" />
              {saveFinalScore.isPending ? "Saving..." : "Save"}
            </Button>
            <Button type="button" onClick={handlePublish} disabled={publishGrade.isPending}>
              <Send aria-hidden="true" />
              {publishGrade.isPending ? "Publishing..." : "Publish"}
            </Button>
            <Button variant="secondary" type="button" onClick={handleRegrade} disabled={regrade.isPending}>
              <RefreshCw aria-hidden="true" />
              {regrade.isPending ? "Regrading..." : "Regrade"}
            </Button>
          </div>
        </section>
      </div>
    </section>
  );
}
