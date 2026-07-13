import { useEffect, useState } from "react";
import { Lock, RefreshCw, Unlock as UnlockIcon } from "lucide-react";
import { Button } from "./ui/Button";
import { FormMessage } from "./ui/FormMessage";
import { StatusBadge } from "./ui/StatusBadge";
import { useConfirmRubric, useRubrics, useUnlockRubric, useUpdateRubricCriteria } from "../hooks/useRubrics";
import type { RubricCriterionInput, RubricListItem } from "../services/rubricService";

function toInputs(rubric: RubricListItem): RubricCriterionInput[] {
  return [...rubric.criteria]
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .map((c) => ({ name: c.name, description: c.description, maxScore: c.maxScore, orderIndex: c.orderIndex }));
}

export function RubricCriteriaPanel({ rubric }: { rubric: RubricListItem }) {
  const { refetch } = useRubrics();
  const updateCriteria = useUpdateRubricCriteria();
  const confirmRubric = useConfirmRubric();
  const unlockRubric = useUnlockRubric();

  const [rows, setRows] = useState<RubricCriterionInput[]>(() => toInputs(rubric));
  const [dirty, setDirty] = useState(false);
  const criteriaSignature = rubric.criteria.map((c) => c.id).join(",");

  useEffect(() => {
    if (!dirty) {
      setRows(toInputs(rubric));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rubric.id, criteriaSignature, dirty]);

  function updateRow(index: number, patch: Partial<RubricCriterionInput>) {
    setRows((prev) => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)));
    setDirty(true);
  }

  function addRow() {
    setRows((prev) => [...prev, { name: "", description: "", maxScore: 0, orderIndex: prev.length }]);
    setDirty(true);
  }

  function deleteRow(index: number) {
    setRows((prev) => prev.filter((_, i) => i !== index).map((row, i) => ({ ...row, orderIndex: i })));
    setDirty(true);
  }

  async function handleSave() {
    await updateCriteria.mutateAsync({ rubricId: rubric.id, criteria: rows });
    setDirty(false);
  }

  async function handleConfirm() {
    await confirmRubric.mutateAsync(rubric.id);
  }

  async function handleUnlock() {
    await unlockRubric.mutateAsync(rubric.id);
  }

  if (rubric.status === "parsing") {
    return (
      <div className="criteria-panel">
        <div className="criteria-panel-header">
          <h3>{rubric.name}</h3>
          <StatusBadge status={rubric.status} />
        </div>
        <p>AI is extracting criteria from the uploaded document...</p>
        <Button variant="secondary" onClick={() => refetch()}>
          <RefreshCw aria-hidden="true" />
          Refresh
        </Button>
      </div>
    );
  }

  const isConfirmed = rubric.status === "confirmed";

  return (
    <div className="criteria-panel">
      <div className="criteria-panel-header">
        <h3>{rubric.name}</h3>
        <StatusBadge status={rubric.status} />
      </div>

      <table className="criteria-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Description</th>
            <th>Max score</th>
            {isConfirmed ? null : <th></th>}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              <td>
                <input
                  value={row.name}
                  onChange={(event) => updateRow(index, { name: event.target.value })}
                  disabled={isConfirmed}
                  placeholder="Criterion name"
                />
              </td>
              <td>
                <input
                  value={row.description ?? ""}
                  onChange={(event) => updateRow(index, { description: event.target.value })}
                  disabled={isConfirmed}
                  placeholder="Description"
                />
              </td>
              <td>
                <input
                  type="number"
                  min={0}
                  value={row.maxScore}
                  onChange={(event) => updateRow(index, { maxScore: Number(event.target.value) })}
                  disabled={isConfirmed}
                />
              </td>
              {isConfirmed ? null : (
                <td>
                  <Button variant="text" onClick={() => deleteRow(index)}>
                    Delete
                  </Button>
                </td>
              )}
            </tr>
          ))}
          {rows.length === 0 ? (
            <tr>
              <td colSpan={4}>No criteria yet.</td>
            </tr>
          ) : null}
        </tbody>
      </table>

      {updateCriteria.error ? <FormMessage tone="error">{updateCriteria.error.message}</FormMessage> : null}
      {confirmRubric.error ? <FormMessage tone="error">{confirmRubric.error.message}</FormMessage> : null}
      {unlockRubric.error ? <FormMessage tone="error">{unlockRubric.error.message}</FormMessage> : null}

      <div className="criteria-panel-actions">
        {isConfirmed ? (
          <Button variant="secondary" onClick={handleUnlock} disabled={unlockRubric.isPending}>
            <UnlockIcon aria-hidden="true" />
            {unlockRubric.isPending ? "Unlocking..." : "Unlock"}
          </Button>
        ) : (
          <>
            <Button variant="secondary" onClick={addRow}>
              Add criterion
            </Button>
            <Button variant="secondary" onClick={handleSave} disabled={!dirty || updateCriteria.isPending}>
              {updateCriteria.isPending ? "Saving..." : "Save"}
            </Button>
            <Button onClick={handleConfirm} disabled={dirty || rows.length === 0 || confirmRubric.isPending}>
              <Lock aria-hidden="true" />
              {confirmRubric.isPending ? "Confirming..." : "Confirm"}
            </Button>
            {dirty ? <span className="criteria-hint">Save changes before confirming.</span> : null}
          </>
        )}
      </div>
    </div>
  );
}
