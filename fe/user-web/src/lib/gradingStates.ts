import type { GradingState } from "./database.types";

export const GRADING_STATES = [
  "uploaded",
  "extracting",
  "extracted",
  "grading",
  "graded",
  "reviewed",
  "published",
  "failed",
] as const satisfies readonly GradingState[];

export const GRADING_STATE_LABELS: Record<GradingState, string> = {
  uploaded: "Uploaded",
  extracting: "Extracting",
  extracted: "Extracted",
  grading: "Grading",
  graded: "AI graded",
  reviewed: "Reviewed",
  published: "Published",
  failed: "Failed",
};

export function canRetry(state: GradingState) {
  return state === "failed" || state === "uploaded" || state === "extracted" || state === "graded";
}

export function isStudentVisibleState(state: GradingState) {
  return state === "published";
}
