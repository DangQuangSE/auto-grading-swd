import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/apiClient";
import * as gradeExportService from "../services/gradeExportService";
import * as rosterService from "../services/rosterService";
import * as submissionService from "../services/submissionService";
import { GradeExportPage } from "./GradeExportPage";

vi.mock("../services/gradeExportService");
vi.mock("../services/rosterService");
vi.mock("../services/submissionService");
vi.mock("xlsx", () => ({
  utils: {
    json_to_sheet: vi.fn(() => ({})),
    book_new: vi.fn(() => ({})),
    book_append_sheet: vi.fn(),
  },
  writeFile: vi.fn(),
}));

const mockedGradeExportService = vi.mocked(gradeExportService);
const mockedRosterService = vi.mocked(rosterService);
const mockedSubmissionService = vi.mocked(submissionService);

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }

  return render(<GradeExportPage />, { wrapper: Wrapper });
}

const assignment = { id: "assignment-1", subjectId: "subject-1", title: "Assignment 1", createdAt: "2026-01-01" };

const submissions = [
  { id: "sub-1", assignmentId: "assignment-1", studentId: "user-a", reportObjectKey: "", diagramObjectKey: "", state: "graded" as const, createdAt: "2026-01-01", updatedAt: "2026-01-01" },
  { id: "sub-2", assignmentId: "assignment-1", studentId: "user-b", reportObjectKey: "", diagramObjectKey: "", state: "graded" as const, createdAt: "2026-01-01", updatedAt: "2026-01-01" },
];

const users = [
  { id: "user-a", email: "a@b.com", fullName: "Alice Nguyen", role: "student", studentCode: "SE100001", classId: "class-1", className: "SE1801" },
  { id: "user-b", email: "b@b.com", fullName: "Bob Tran", role: "student", studentCode: "SE100002", classId: "class-2", className: "SE1802" },
];

beforeEach(() => {
  vi.resetAllMocks();
  mockedGradeExportService.getAssignments.mockResolvedValue([assignment]);
  mockedSubmissionService.listAssignmentSubmissions.mockResolvedValue(submissions);
  mockedRosterService.getUsersByIds.mockResolvedValue(users);
  mockedGradeExportService.batchGetGrades.mockResolvedValue([
    { submissionId: "sub-1", finalGradeId: "grade-1", finalScore: 9.5, createdAt: "2026-01-02" },
  ]);
});

describe("GradeExportPage", () => {
  it("shows a prompt before an assignment is selected", async () => {
    renderPage();

    expect(await screen.findByText("Assignment 1")).toBeInTheDocument();
    expect(screen.getByText("Select an assignment to view grades")).toBeInTheDocument();
  });

  it("loads and joins the grade table after selecting an assignment", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");

    expect(await screen.findByText("Alice Nguyen")).toBeInTheDocument();
    expect(screen.getByText("SE100001")).toBeInTheDocument();
    expect(screen.getByText("SE1801")).toBeInTheDocument();
    expect(screen.getByText("9.5")).toBeInTheDocument();
    expect(screen.getByText("Bob Tran")).toBeInTheDocument();
    expect(screen.getByText("Not graded")).toBeInTheDocument();
  });

  it("filters by MSSV alone", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");
    await screen.findByText("Alice Nguyen");

    await user.type(screen.getByPlaceholderText("SE123456"), "SE100001");

    expect(screen.getByText("Alice Nguyen")).toBeInTheDocument();
    expect(screen.queryByText("Bob Tran")).not.toBeInTheDocument();
  });

  it("filters by class alone", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");
    await screen.findByText("Alice Nguyen");

    await user.type(screen.getByPlaceholderText("SE1801"), "SE1802");

    expect(screen.queryByText("Alice Nguyen")).not.toBeInTheDocument();
    expect(screen.getByText("Bob Tran")).toBeInTheDocument();
  });

  it("combines MSSV and class filters with AND logic", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");
    await screen.findByText("Alice Nguyen");

    await user.type(screen.getByPlaceholderText("SE123456"), "SE100001");
    await user.type(screen.getByPlaceholderText("SE1801"), "SE1802");

    expect(screen.getByText("No results match the current filters")).toBeInTheDocument();
  });

  it("shows an empty state when the assignment has no submissions", async () => {
    mockedSubmissionService.listAssignmentSubmissions.mockResolvedValue([]);

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");

    expect(await screen.findByText("No submissions for this assignment")).toBeInTheDocument();
  });

  it("shows an error and a retry button on a server error", async () => {
    mockedSubmissionService.listAssignmentSubmissions.mockRejectedValue(new ApiError(500, "Internal error"));

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");

    expect(await screen.findByText("Something went wrong loading grades. Please try again.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /retry/i })).toBeInTheDocument();
  });

  it("exports the filtered rows to Excel", async () => {
    const xlsx = await import("xlsx");
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");
    await screen.findByText("Alice Nguyen");

    await user.type(screen.getByPlaceholderText("SE123456"), "SE100001");
    await user.click(screen.getByRole("button", { name: /export to excel/i }));

    await waitFor(() => {
      expect(xlsx.utils.json_to_sheet).toHaveBeenCalledWith([
        { "Student Name": "Alice Nguyen", MSSV: "SE100001", "Class Name": "SE1801", "Final Score": 9.5 },
      ]);
    });
    expect(xlsx.writeFile).toHaveBeenCalledWith(expect.anything(), expect.stringMatching(/^assignment-1-grades-\d{4}-\d{2}-\d{2}\.xlsx$/));
  });

  it("neutralizes formula-injection characters in exported student data", async () => {
    mockedRosterService.getUsersByIds.mockResolvedValue([
      { id: "user-a", email: "a@b.com", fullName: "=cmd|'/c calc'!A1", role: "student", studentCode: "+SE1", classId: "class-1", className: "@SE1801" },
      users[1],
    ]);
    const xlsx = await import("xlsx");
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Assignment 1");
    await user.selectOptions(screen.getByLabelText("Assignment"), "assignment-1");
    await screen.findByText("Bob Tran");
    await user.click(screen.getByRole("button", { name: /export to excel/i }));

    await waitFor(() => {
      expect(xlsx.utils.json_to_sheet).toHaveBeenCalledWith(
        expect.arrayContaining([
          expect.objectContaining({
            "Student Name": "'=cmd|'/c calc'!A1",
            MSSV: "'+SE1",
            "Class Name": "'@SE1801",
          }),
        ]),
      );
    });
  });
});
