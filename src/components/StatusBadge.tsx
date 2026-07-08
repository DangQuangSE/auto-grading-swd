import type { GradingState } from "../lib/database.types";
import { GRADING_STATE_LABELS } from "../lib/gradingStates";

export function StatusBadge({ state }: { state: GradingState }) {
  return <span className={`status-badge state-${state}`}>{GRADING_STATE_LABELS[state]}</span>;
}
