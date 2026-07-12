import type { RubricStatus } from "../../services/rubricService";

const LABELS: Record<RubricStatus, string> = {
  parsing: "Parsing",
  draft: "Draft",
  confirmed: "Confirmed",
};

export function StatusBadge({ status }: { status: RubricStatus }) {
  return <span className={`status-badge status-badge--${status}`}>{LABELS[status]}</span>;
}
