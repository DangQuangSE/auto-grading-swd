import { useState } from "react";
import { ArrowLeft, CheckCircle2, Upload, XCircle } from "lucide-react";
import { Link } from "react-router-dom";
import { FileDropzone } from "../components/FileDropzone";
import { Button } from "../components/ui/Button";
import { FormMessage } from "../components/ui/FormMessage";
import { StateBlock } from "../components/ui/StateBlock";
import { useUploadRosterFile } from "../hooks/useBulkImport";
import { ApiError } from "../lib/apiClient";
import { assertValidFileExtension, assertValidFileSize } from "../lib/validation";
import { previewRosterFile, type RosterImportRowResult, type RosterPreviewRow } from "../services/bulkImportService";

const ALLOWED_EXTENSIONS = [".xlsx", ".xls", ".csv"];
const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024;

type ReportFilter = "all" | "updated" | "skipped";

function describeUploadError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 400) {
      return `Invalid file format: ${error.message}`;
    }
    if (error.status >= 500) {
      return "Upload failed. Please try again or contact support.";
    }
    return error.message || "Upload failed. Please try again.";
  }
  return "Connection lost. Please check your internet and retry.";
}

function matchesReportFilter(row: RosterImportRowResult, filter: ReportFilter): boolean {
  return filter === "all" || row.status === filter;
}

export function BulkImportPage() {
  const [file, setFile] = useState<File | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [preview, setPreview] = useState<RosterPreviewRow[]>([]);
  const [reportFilter, setReportFilter] = useState<ReportFilter>("all");

  const uploadRosterFile = useUploadRosterFile();

  async function handleFileChange(nextFile: File | null) {
    uploadRosterFile.reset();
    setPreview([]);
    setFile(nextFile);
    setValidationError(null);

    if (!nextFile) {
      return;
    }

    try {
      assertValidFileExtension(nextFile.name, ALLOWED_EXTENSIONS);
      assertValidFileSize(nextFile.size, MAX_FILE_SIZE_BYTES);
    } catch (error) {
      setFile(null);
      setValidationError(error instanceof Error ? error.message : "Invalid file.");
      return;
    }

    setPreview(await previewRosterFile(nextFile));
  }

  async function handleUpload() {
    if (!file) {
      return;
    }

    try {
      await uploadRosterFile.mutateAsync(file);
      setFile(null);
      setPreview([]);
    } catch {
      // surfaced via uploadRosterFile.error below
    }
  }

  const report = uploadRosterFile.data ?? null;
  const filteredDetails = report?.details.filter((row) => matchesReportFilter(row, reportFilter)) ?? [];

  return (
    <section className="page-grid compact-page">
      <header className="page-header">
        <p>Bulk import</p>
        <h1>Import student roster</h1>
        <Link to="/roster" className="secondary-button">
          <ArrowLeft aria-hidden="true" />
          Back to roster
        </Link>
      </header>
      <div className="form-panel">
        <p>File must contain columns: Email, StudentCode, ClassName (any order, header names case-insensitive).</p>
        <FileDropzone label="Roster file" accept={ALLOWED_EXTENSIONS.join(",")} file={file} onChange={handleFileChange} />
        {validationError ? <FormMessage tone="error">{validationError}</FormMessage> : null}
        {preview.length > 0 ? (
          <div className="table-panel">
            <p>Preview: first {preview.length} rows shown</p>
            <table>
              <thead>
                <tr>
                  <th>Email</th>
                  <th>StudentCode</th>
                  <th>ClassName</th>
                </tr>
              </thead>
              <tbody>
                {preview.map((row, index) => (
                  <tr key={index}>
                    <td>{row.email}</td>
                    <td>{row.studentCode}</td>
                    <td>{row.className}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
        {uploadRosterFile.error ? <FormMessage tone="error">{describeUploadError(uploadRosterFile.error)}</FormMessage> : null}
        <Button type="button" onClick={handleUpload} disabled={!file || uploadRosterFile.isPending}>
          <Upload aria-hidden="true" />
          {uploadRosterFile.isPending ? "Uploading..." : "Upload"}
        </Button>
      </div>

      {report ? (
        <div className="table-panel">
          <FormMessage tone="success">
            {`Updated ${report.updatedCount} student${report.updatedCount === 1 ? "" : "s"}. ${report.skippedCount} row${report.skippedCount === 1 ? "" : "s"} were skipped.`}
          </FormMessage>
          <div className="criteria-panel-actions">
            <Button type="button" variant={reportFilter === "all" ? "primary" : "secondary"} onClick={() => setReportFilter("all")}>
              All ({report.totalRows})
            </Button>
            <Button
              type="button"
              variant={reportFilter === "updated" ? "primary" : "secondary"}
              onClick={() => setReportFilter("updated")}
            >
              Updated ({report.updatedCount})
            </Button>
            <Button
              type="button"
              variant={reportFilter === "skipped" ? "primary" : "secondary"}
              onClick={() => setReportFilter("skipped")}
            >
              Skipped ({report.skippedCount})
            </Button>
          </div>
          {filteredDetails.length === 0 ? <StateBlock title="No rows match this filter" /> : null}
          {filteredDetails.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Row</th>
                  <th>Email</th>
                  <th>Status</th>
                  <th>Reason</th>
                </tr>
              </thead>
              <tbody>
                {filteredDetails.map((row) => (
                  <tr key={row.rowNumber}>
                    <td>{row.rowNumber}</td>
                    <td>{row.email}</td>
                    <td>
                      {row.status === "updated" ? (
                        <CheckCircle2 aria-label="Updated" color="#12613d" />
                      ) : (
                        <XCircle aria-label="Skipped" color="#8a1f1f" />
                      )}
                    </td>
                    <td>{row.reason ?? "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
