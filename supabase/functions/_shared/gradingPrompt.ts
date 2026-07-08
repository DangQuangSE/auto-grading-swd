export type PromptCriterion = {
  id: string;
  criterion_code: string;
  title: string;
  description: string;
  max_score: number;
  grading_guidance: string;
  deduction_notes: string;
};

export type GradingPromptInput = {
  criteria: PromptCriterion[];
  documentArtifact: unknown;
  diagramArtifact: unknown;
  extractionWarnings: unknown[];
};

export function buildGradingPrompt(input: GradingPromptInput) {
  return [
    {
      role: "system",
      content:
        "You are an assistant for university lecturers grading IT project documents. You must grade strictly by rubric, cite evidence, identify deductions, and never exceed max_score. Return only valid JSON.",
    },
    {
      role: "user",
      content: JSON.stringify(
        {
          task: "Grade this submission criterion by criterion. The lecturer will review your suggestions before publication.",
          output_schema: {
            criterion_scores: [
              {
                criterion_id: "uuid from rubric criterion",
                max_score: "number copied from rubric",
                suggested_score: "number from 0 to max_score",
                deductions: [{ reason: "string", points: "number" }],
                evidence: [
                  {
                    source: "document | diagram | rubric | missing",
                    reference: "section/entity/relationship/criterion reference",
                    quote: "short optional quote",
                  },
                ],
                comment: "specific feedback for lecturer/student",
                confidence: "low | medium | high",
              },
            ],
            overall_comment: "brief summary",
          },
          grading_rules: [
            "Score every criterion exactly once.",
            "Do not award more than max_score for any criterion.",
            "If evidence is missing, use source='missing' and explain the deduction.",
            "Prefer concrete evidence over generic comments.",
            "Do not invent facts that are not in the extracted document or diagram.",
          ],
          rubric_criteria: input.criteria,
          extracted_document: input.documentArtifact,
          extracted_diagram: input.diagramArtifact,
          extraction_warnings: input.extractionWarnings,
        },
        null,
        2,
      ),
    },
  ];
}
