export type ParsedAiCriterionScore = {
  criterionId: string;
  maxScore: number;
  suggestedScore: number;
  deductions: Array<{ reason: string; points: number }>;
  evidence: Array<{
    source: "document" | "diagram" | "rubric" | "missing";
    reference: string;
    quote?: string;
  }>;
  comment: string;
  confidence: "low" | "medium" | "high";
};

export type ParsedAiGrading = {
  criterionScores: ParsedAiCriterionScore[];
  overallComment: string;
};

type RubricCriterionLimit = {
  id: string;
  max_score: number;
};

function extractJson(raw: string) {
  const trimmed = raw.trim();
  if (trimmed.startsWith("{")) {
    return trimmed;
  }

  const fenced = trimmed.match(/```(?:json)?\s*([\s\S]*?)```/i);
  if (fenced) {
    return fenced[1].trim();
  }

  const firstBrace = trimmed.indexOf("{");
  const lastBrace = trimmed.lastIndexOf("}");
  if (firstBrace >= 0 && lastBrace > firstBrace) {
    return trimmed.slice(firstBrace, lastBrace + 1);
  }

  throw new Error("AI response did not contain a JSON object.");
}

function asNumber(value: unknown, fallback = 0) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function asString(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function asArray(value: unknown) {
  return Array.isArray(value) ? value : [];
}

export function parseAiGradingResponse(raw: string, criteria: RubricCriterionLimit[]): ParsedAiGrading {
  const parsed = JSON.parse(extractJson(raw)) as Record<string, unknown>;
  const criterionScoresRaw = asArray(parsed.criterion_scores);
  const limits = new Map(criteria.map((criterion) => [criterion.id, criterion.max_score]));

  const criterionScores = criterionScoresRaw.map((item) => {
    const record = item as Record<string, unknown>;
    const criterionId = asString(record.criterion_id);
    const rubricMaxScore = limits.get(criterionId);

    if (rubricMaxScore === undefined) {
      throw new Error(`AI response referenced unknown criterion_id: ${criterionId || "(blank)"}.`);
    }

    const responseMaxScore = asNumber(record.max_score, rubricMaxScore);
    const maxScore = Math.min(responseMaxScore, rubricMaxScore);
    const suggestedScore = asNumber(record.suggested_score);

    if (suggestedScore > maxScore) {
      throw new Error(`AI suggested score exceeds max score for criterion ${criterionId}.`);
    }

    return {
      criterionId,
      maxScore,
      suggestedScore,
      deductions: asArray(record.deductions).map((deduction) => {
        const deductionRecord = deduction as Record<string, unknown>;
        return {
          reason: asString(deductionRecord.reason, "No reason provided."),
          points: asNumber(deductionRecord.points),
        };
      }),
      evidence: asArray(record.evidence).map((evidence) => {
        const evidenceRecord = evidence as Record<string, unknown>;
        const source = asString(evidenceRecord.source, "missing");
        return {
          source: ["document", "diagram", "rubric", "missing"].includes(source)
            ? (source as ParsedAiCriterionScore["evidence"][number]["source"])
            : "missing",
          reference: asString(evidenceRecord.reference, "No reference provided."),
          quote: asString(evidenceRecord.quote) || undefined,
        };
      }),
      comment: asString(record.comment),
      confidence: ["low", "medium", "high"].includes(asString(record.confidence))
        ? (asString(record.confidence) as ParsedAiCriterionScore["confidence"])
        : "medium",
    };
  });

  const missingCriteria = criteria.filter(
    (criterion) => !criterionScores.some((score) => score.criterionId === criterion.id),
  );

  if (missingCriteria.length > 0) {
    throw new Error(`AI response missed criteria: ${missingCriteria.map((criterion) => criterion.id).join(", ")}`);
  }

  return {
    criterionScores,
    overallComment: asString(parsed.overall_comment),
  };
}
