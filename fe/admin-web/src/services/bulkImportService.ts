import { read, utils } from "xlsx";
import { apiPostForm } from "../lib/apiClient";

export type RosterImportRowResult = {
  rowNumber: number;
  email: string;
  status: "updated" | "skipped";
  reason: string | null;
};

export type RosterImportReport = {
  totalRows: number;
  updatedCount: number;
  skippedCount: number;
  details: RosterImportRowResult[];
};

export async function uploadRosterFile(file: File) {
  const form = new FormData();
  form.set("File", file);

  return apiPostForm<RosterImportReport>("/identity/users/bulk-import", form);
}

export type RosterPreviewRow = {
  email: string;
  studentCode: string;
  className: string;
};

const PREVIEW_ROW_COUNT = 5;
const REQUIRED_COLUMNS = ["Email", "StudentCode", "ClassName"] as const;

/** Parses the first few data rows client-side for a non-blocking preview — the server's own
 * parse (RosterFileParser.cs) is the source of truth; a mismatch here just means a misleading
 * preview, not a bad import. Columns are matched by header name, case-insensitively. */
export async function previewRosterFile(file: File): Promise<RosterPreviewRow[]> {
  const buffer = await file.arrayBuffer();
  const workbook = read(buffer, { type: "array" });
  const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
  if (!firstSheet) {
    return [];
  }

  const rows = utils.sheet_to_json<string[]>(firstSheet, { header: 1, blankrows: false });
  const [header, ...dataRows] = rows;
  if (!header) {
    return [];
  }

  const columnIndex = Object.fromEntries(
    REQUIRED_COLUMNS.map((name) => [
      name,
      header.findIndex((cell) => String(cell).trim().toLowerCase() === name.toLowerCase()),
    ]),
  );

  return dataRows.slice(0, PREVIEW_ROW_COUNT).map((row) => ({
    email: cell(row, columnIndex.Email),
    studentCode: cell(row, columnIndex.StudentCode),
    className: cell(row, columnIndex.ClassName),
  }));
}

function cell(row: string[], index: number): string {
  if (index < 0 || row[index] == null) {
    return "";
  }
  return String(row[index]).trim();
}
