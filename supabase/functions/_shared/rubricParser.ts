import type { ParsedDocx } from "./docxParser.ts";

export type NormalizedRubricCriterion = {
  criterionCode: string;
  title: string;
  description: string;
  maxScore: number;
  gradingGuidance: string;
  deductionNotes: string;
  displayOrder: number;
};

export type ParsedRubric = {
  criteria: NormalizedRubricCriterion[];
  warnings: string[];
};

const HEADER_ALIASES = {
  code: ["ma tieu chi", "ma", "criterion", "criterion code", "id"],
  title: ["noi dung can cham", "tieu chi", "title", "noi dung", "cau hoi"],
  description: ["mo ta", "description", "yeu cau", "chi tiet"],
  maxScore: ["diem toi da", "max score", "score", "diem"],
  guidance: ["muc dat", "huong dan cham", "grading guidance", "rubric"],
  deductions: ["loi tru diem", "deduction", "deductions", "ghi chu"],
};

function normalizeHeader(value: string) {
  return value
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^\w\s]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function findColumn(headers: string[], aliases: string[]) {
  const exactMatch = headers.findIndex((header) => aliases.some((alias) => header === alias));
  if (exactMatch >= 0) {
    return exactMatch;
  }

  return headers.findIndex((header) => aliases.some((alias) => header.includes(alias)));
}

function parseScore(value: string) {
  const match = value.replace(",", ".").match(/\d+(\.\d+)?/);
  return match ? Number(match[0]) : Number.NaN;
}

export function parseRubricFromDocx(parsedDocx: ParsedDocx): ParsedRubric {
  const warnings = [...parsedDocx.warnings];
  const table = parsedDocx.tables.find((candidate) => candidate.length >= 2 && candidate[0].length >= 4);

  if (!table) {
    throw new Error("Rubric .docx must contain a table with at least one header row and one criterion row.");
  }

  const headers = table[0].map(normalizeHeader);
  const codeColumn = findColumn(headers, HEADER_ALIASES.code);
  const titleColumn = findColumn(headers, HEADER_ALIASES.title);
  const descriptionColumn = findColumn(headers, HEADER_ALIASES.description);
  const maxScoreColumn = findColumn(headers, HEADER_ALIASES.maxScore);
  const guidanceColumn = findColumn(headers, HEADER_ALIASES.guidance);
  const deductionsColumn = findColumn(headers, HEADER_ALIASES.deductions);

  const missingColumns = [
    ["criterion code", codeColumn],
    ["title", titleColumn],
    ["description", descriptionColumn],
    ["max score", maxScoreColumn],
  ].filter(([, index]) => index === -1);

  if (missingColumns.length > 0) {
    throw new Error(`Rubric table is missing required columns: ${missingColumns.map(([name]) => name).join(", ")}`);
  }

  const criteria = table.slice(1).flatMap((row, index) => {
    const maxScore = parseScore(row[maxScoreColumn] ?? "");

    if (!row.some(Boolean)) {
      return [];
    }

    if (!Number.isFinite(maxScore)) {
      warnings.push(`Rubric row ${index + 2} was skipped because max score is invalid.`);
      return [];
    }

    return [
      {
        criterionCode: row[codeColumn]?.trim() || `C${index + 1}`,
        title: row[titleColumn]?.trim() || `Criterion ${index + 1}`,
        description: row[descriptionColumn]?.trim() || "",
        maxScore,
        gradingGuidance: guidanceColumn >= 0 ? row[guidanceColumn]?.trim() || "" : "",
        deductionNotes: deductionsColumn >= 0 ? row[deductionsColumn]?.trim() || "" : "",
        displayOrder: index,
      },
    ];
  });

  if (criteria.length === 0) {
    throw new Error("Rubric table did not contain any valid criteria.");
  }

  return { criteria, warnings };
}
