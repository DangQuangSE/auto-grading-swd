import { z } from "zod";
import { GRADING_STATES } from "./gradingStates";

const nonEmptyString = z.string().trim().min(1);
const score = z.number().finite().nonnegative();

export const rubricCriterionSchema = z.object({
  id: z.string().uuid().optional(),
  criterionCode: nonEmptyString,
  title: nonEmptyString,
  description: nonEmptyString,
  maxScore: score,
  gradingGuidance: z.string().default(""),
  deductionNotes: z.string().default(""),
  displayOrder: z.number().int().nonnegative().default(0),
});

export const rubricTemplateSchema = z.object({
  id: z.string().uuid().optional(),
  subjectId: z.string().uuid(),
  assignmentId: z.string().uuid().nullable().optional(),
  version: z.number().int().positive().default(1),
  originalFilename: nonEmptyString,
  filePath: nonEmptyString,
  criteria: z.array(rubricCriterionSchema).min(1),
});

export const submissionMetadataSchema = z.object({
  assignmentId: z.string().uuid(),
  studentId: z.string().uuid(),
  rubricId: z.string().uuid().nullable().optional(),
  state: z.enum(GRADING_STATES).default("uploaded"),
  reportOriginalFilename: nonEmptyString,
  diagramOriginalFilename: nonEmptyString,
});

export const extractionArtifactSchema = z.object({
  submissionId: z.string().uuid(),
  artifactType: z.enum(["rubric", "document", "diagram"]),
  content: z.record(z.unknown()).default({}),
  warnings: z.array(z.string()).default([]),
});

export const criterionDeductionSchema = z.object({
  reason: nonEmptyString,
  points: score,
});

export const criterionEvidenceSchema = z.object({
  source: z.enum(["document", "diagram", "rubric", "missing"]),
  reference: nonEmptyString,
  quote: z.string().optional(),
});

export const aiCriterionScoreSchema = z
  .object({
    criterionId: z.string().uuid(),
    maxScore: score,
    suggestedScore: score,
    deductions: z.array(criterionDeductionSchema).default([]),
    evidence: z.array(criterionEvidenceSchema).default([]),
    comment: z.string().default(""),
    confidence: z.enum(["low", "medium", "high"]).default("medium"),
  })
  .superRefine((value, ctx) => {
    if (value.suggestedScore > value.maxScore) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["suggestedScore"],
        message: "Suggested score cannot exceed max score.",
      });
    }
  });

export const finalCriterionScoreSchema = z
  .object({
    criterionId: z.string().uuid(),
    aiCriterionScoreId: z.string().uuid().nullable().optional(),
    finalScore: score,
    finalComment: z.string().default(""),
    maxScore: score,
  })
  .superRefine((value, ctx) => {
    if (value.finalScore > value.maxScore) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["finalScore"],
        message: "Final score cannot exceed max score.",
      });
    }
  });

export function assertValidFileExtension(fileName: string, allowedExtensions: string[]) {
  const normalized = fileName.toLowerCase();
  const matches = allowedExtensions.some((extension) => normalized.endsWith(extension));

  if (!matches) {
    throw new Error(`File must use one of these extensions: ${allowedExtensions.join(", ")}`);
  }
}

export function assertValidFileSize(sizeInBytes: number, maxSizeInBytes: number) {
  if (sizeInBytes > maxSizeInBytes) {
    throw new Error(`File is too large (max ${Math.floor(maxSizeInBytes / 1024 / 1024)} MB).`);
  }
}

const FORMULA_TRIGGER_CHARS = ["=", "+", "-", "@", "\t", "\r"];

/** Neutralizes CSV/Excel formula injection (CWE-1236): a cell value starting with one of these
 * characters can be interpreted as a formula by some spreadsheet apps when the sheet is opened.
 * Prefixing with a single quote forces spreadsheet apps to treat it as literal text. */
export function sanitizeSpreadsheetCell(value: string): string {
  return FORMULA_TRIGGER_CHARS.includes(value[0]) ? `'${value}` : value;
}

export function validateAiScoreWithinCriterion(suggestedScore: number, maxScore: number) {
  return aiCriterionScoreSchema.parse({
    criterionId: "00000000-0000-4000-8000-000000000000",
    maxScore,
    suggestedScore,
    deductions: [],
    evidence: [
      {
        source: "missing",
        reference: "validation-only",
      },
    ],
    comment: "",
    confidence: "medium",
  });
}
