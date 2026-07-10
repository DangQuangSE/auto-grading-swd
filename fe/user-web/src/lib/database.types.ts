export type Json =
  | string
  | number
  | boolean
  | null
  | { [key: string]: Json | undefined }
  | Json[];

export type AppRole = "student" | "lecturer" | "admin";

export type GradingState =
  | "uploaded"
  | "extracting"
  | "extracted"
  | "grading"
  | "graded"
  | "reviewed"
  | "published"
  | "failed";

export type ArtifactType = "rubric" | "document" | "diagram";

export type AuditEventType =
  | "file_uploaded"
  | "rubric_uploaded"
  | "extraction_started"
  | "extraction_completed"
  | "extraction_failed"
  | "ai_grading_started"
  | "ai_grading_completed"
  | "ai_grading_failed"
  | "lecturer_review_saved"
  | "grade_published"
  | "retry_requested";

