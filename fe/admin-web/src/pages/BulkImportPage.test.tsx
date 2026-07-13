import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/apiClient";
import * as bulkImportService from "../services/bulkImportService";
import { BulkImportPage } from "./BulkImportPage";

vi.mock("../services/bulkImportService", async () => {
  const actual = await vi.importActual<typeof import("../services/bulkImportService")>("../services/bulkImportService");
  return {
    ...actual,
    uploadRosterFile: vi.fn(),
    previewRosterFile: vi.fn(),
  };
});

const mockedBulkImportService = vi.mocked(bulkImportService);

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>{children}</MemoryRouter>
      </QueryClientProvider>
    );
  }

  return render(<BulkImportPage />, { wrapper: Wrapper });
}

function makeFile(name: string, sizeInBytes = 1024, type = "text/csv") {
  const file = new File(["email,studentCode,className\na@b.com,SE1,SE1801"], name, { type });
  Object.defineProperty(file, "size", { value: sizeInBytes });
  return file;
}

beforeEach(() => {
  vi.resetAllMocks();
  mockedBulkImportService.previewRosterFile.mockResolvedValue([
    { email: "a@b.com", studentCode: "SE1", className: "SE1801" },
  ]);
});

describe("BulkImportPage", () => {
  it("renders instructions and file input", () => {
    renderPage();

    expect(screen.getByText(/must contain columns/i)).toBeInTheDocument();
  });

  it("rejects a file with an invalid extension", async () => {
    renderPage();

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    // fireEvent bypasses userEvent's accept-attribute filtering, simulating a drag-and-drop
    // of a file the OS file picker would otherwise have hidden.
    fireEvent.change(input, { target: { files: [makeFile("roster.pdf")] } });

    expect(await screen.findByText(/must use one of these extensions/i)).toBeInTheDocument();
    expect(mockedBulkImportService.previewRosterFile).not.toHaveBeenCalled();
  });

  it("rejects a file larger than the size limit", async () => {
    const user = userEvent.setup();
    renderPage();

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, makeFile("roster.csv", 11 * 1024 * 1024));

    expect(await screen.findByText(/too large/i)).toBeInTheDocument();
  });

  it("shows a preview after selecting a valid file", async () => {
    const user = userEvent.setup();
    renderPage();

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, makeFile("roster.csv"));

    expect(await screen.findByText("a@b.com")).toBeInTheDocument();
    expect(screen.getByText("SE1801")).toBeInTheDocument();
  });

  it("uploads the file and displays the report with mixed results", async () => {
    mockedBulkImportService.uploadRosterFile.mockResolvedValue({
      totalRows: 3,
      updatedCount: 2,
      skippedCount: 1,
      details: [
        { rowNumber: 1, email: "a@b.com", status: "updated", reason: null },
        { rowNumber: 2, email: "c@d.com", status: "updated", reason: null },
        { rowNumber: 3, email: "e@f.com", status: "skipped", reason: "unknown class" },
      ],
    });

    const user = userEvent.setup();
    renderPage();

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, makeFile("roster.csv"));
    await screen.findByText("a@b.com");

    await user.click(screen.getByRole("button", { name: /^upload$/i }));

    expect(await screen.findByText("Updated 2 students. 1 row were skipped.")).toBeInTheDocument();
    expect(screen.getByText("unknown class")).toBeInTheDocument();
  });

  it("filters the report to skipped rows only", async () => {
    mockedBulkImportService.uploadRosterFile.mockResolvedValue({
      totalRows: 2,
      updatedCount: 1,
      skippedCount: 1,
      details: [
        { rowNumber: 1, email: "a@b.com", status: "updated", reason: null },
        { rowNumber: 2, email: "e@f.com", status: "skipped", reason: "email not registered" },
      ],
    });

    const user = userEvent.setup();
    renderPage();

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, makeFile("roster.csv"));
    await screen.findByText("a@b.com");
    await user.click(screen.getByRole("button", { name: /^upload$/i }));
    await screen.findByText("email not registered");

    await user.click(screen.getByRole("button", { name: /^skipped/i }));

    expect(screen.queryByText("a@b.com")).not.toBeInTheDocument();
    expect(screen.getByText("e@f.com")).toBeInTheDocument();
  });

  it("shows an error message when the upload fails with a 400", async () => {
    mockedBulkImportService.uploadRosterFile.mockRejectedValue(new ApiError(400, "missing column Email"));

    const user = userEvent.setup();
    renderPage();

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, makeFile("roster.csv"));
    await screen.findByText("a@b.com");
    await user.click(screen.getByRole("button", { name: /^upload$/i }));

    expect(await screen.findByText("Invalid file format: missing column Email")).toBeInTheDocument();
  });

  it("shows a generic message when the upload fails with a 500", async () => {
    mockedBulkImportService.uploadRosterFile.mockRejectedValue(new ApiError(500, "Internal error"));

    const user = userEvent.setup();
    renderPage();

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, makeFile("roster.csv"));
    await screen.findByText("a@b.com");
    await user.click(screen.getByRole("button", { name: /^upload$/i }));

    await waitFor(() => {
      expect(screen.getByText("Upload failed. Please try again or contact support.")).toBeInTheDocument();
    });
  });
});
