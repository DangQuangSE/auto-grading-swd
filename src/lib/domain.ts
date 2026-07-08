import type { ArtifactType, GradingState } from "./database.types";

export type RubricCriterion = {
  id?: string;
  criterionCode: string;
  title: string;
  description: string;
  maxScore: number;
  gradingGuidance: string;
  deductionNotes: string;
  displayOrder: number;
};

export type RubricTemplate = {
  id?: string;
  subjectId: string;
  assignmentId?: string | null;
  version: number;
  originalFilename: string;
  filePath: string;
  criteria: RubricCriterion[];
};

export type SubmissionFileSet = {
  reportFile: File;
  diagramFile: File;
};

export type SubmissionSummary = {
  id: string;
  assignmentId: string;
  studentId: string;
  state: GradingState;
  reportOriginalFilename: string;
  diagramOriginalFilename: string;
  failureReason?: string | null;
};

export type DocumentSection = {
  heading: string;
  text: string;
  order: number;
};

export type DiagramEntity = {
  id: string;
  label: string;
  type?: string;
};

export type DiagramRelationship = {
  id: string;
  sourceId?: string;
  targetId?: string;
  label?: string;
};

export type ExtractionArtifact = {
  submissionId: string;
  artifactType: ArtifactType;
  content: Record<string, unknown>;
  warnings: string[];
};

export type CriterionDeduction = {
  reason: string;
  points: number;
};

export type CriterionEvidence = {
  source: "document" | "diagram" | "rubric" | "missing";
  reference: string;
  quote?: string;
};

export type AiCriterionScore = {
  criterionId: string;
  maxScore: number;
  suggestedScore: number;
  deductions: CriterionDeduction[];
  evidence: CriterionEvidence[];
  comment: string;
  confidence: "low" | "medium" | "high";
};

export type FinalCriterionScore = {
  criterionId: string;
  aiCriterionScoreId?: string | null;
  finalScore: number;
  finalComment: string;
};
