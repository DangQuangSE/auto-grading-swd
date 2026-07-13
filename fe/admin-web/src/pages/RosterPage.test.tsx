import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/apiClient";
import * as classService from "../services/classService";
import * as rosterService from "../services/rosterService";
import { RosterPage } from "./RosterPage";

vi.mock("../services/rosterService");
vi.mock("../services/classService");

const mockedRosterService = vi.mocked(rosterService);
const mockedClassService = vi.mocked(classService);

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

  return render(<RosterPage />, { wrapper: Wrapper });
}

const studentA = {
  id: "user-a",
  email: "alice@school.edu",
  fullName: "Alice Nguyen",
  role: "student",
  studentCode: "SE100001",
  classId: "class-1",
  className: "SE1801",
};
const studentB = {
  id: "user-b",
  email: "bob@school.edu",
  fullName: "Bob Tran",
  role: "student",
  studentCode: null,
  classId: null,
  className: null,
};

beforeEach(() => {
  vi.resetAllMocks();
  mockedClassService.getClasses.mockResolvedValue([{ id: "class-1", name: "SE1801", lecturerId: "lecturer-a" }]);
});

describe("RosterPage", () => {
  it("renders the student list", async () => {
    mockedRosterService.listUsers.mockResolvedValue([studentA, studentB]);

    renderPage();

    expect(await screen.findByText("alice@school.edu")).toBeInTheDocument();
    expect(screen.getByText("bob@school.edu")).toBeInTheDocument();
    expect(screen.getByText("SE100001")).toBeInTheDocument();
  });

  it("filters students by email", async () => {
    mockedRosterService.listUsers.mockResolvedValue([studentA, studentB]);

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("alice@school.edu");
    await user.type(screen.getByPlaceholderText("student@school.edu"), "alice");

    expect(screen.getByText("alice@school.edu")).toBeInTheDocument();
    expect(screen.queryByText("bob@school.edu")).not.toBeInTheDocument();
  });

  it("filters students by MSSV", async () => {
    mockedRosterService.listUsers.mockResolvedValue([studentA, studentB]);

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("alice@school.edu");
    await user.type(screen.getByPlaceholderText("SE123456"), "SE1000");

    expect(screen.getByText("alice@school.edu")).toBeInTheDocument();
    expect(screen.queryByText("bob@school.edu")).not.toBeInTheDocument();
  });

  it("opens the edit modal and saves an update", async () => {
    mockedRosterService.listUsers.mockResolvedValue([studentA]);
    mockedRosterService.updateUser.mockResolvedValue({ ...studentA, studentCode: "SE999999" });

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("alice@school.edu");
    await user.click(screen.getByRole("button", { name: /edit/i }));

    const dialog = screen.getByRole("dialog");
    const mssvInput = within(dialog).getByPlaceholderText("SE123456");
    await user.clear(mssvInput);
    await user.type(mssvInput, "SE999999");
    await user.click(within(dialog).getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(mockedRosterService.updateUser).toHaveBeenCalledWith("user-a", {
        studentCode: "SE999999",
        classId: "class-1",
      });
    });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("shows a 403 error message in the modal", async () => {
    mockedRosterService.listUsers.mockResolvedValue([studentA]);
    mockedRosterService.updateUser.mockRejectedValue(new ApiError(403, "Forbidden"));

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("alice@school.edu");
    await user.click(screen.getByRole("button", { name: /edit/i }));
    await user.click(screen.getByRole("button", { name: /save/i }));

    expect(await screen.findByText("You are not authorized to edit this student.")).toBeInTheDocument();
  });

  it("shows the server-provided message on a 400 error", async () => {
    mockedRosterService.listUsers.mockResolvedValue([studentA]);
    mockedRosterService.updateUser.mockRejectedValue(new ApiError(400, "Class not found or not yet synchronized."));

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("alice@school.edu");
    await user.click(screen.getByRole("button", { name: /edit/i }));
    await user.click(screen.getByRole("button", { name: /save/i }));

    expect(await screen.findByText("Class not found or not yet synchronized.")).toBeInTheDocument();
  });

  it("shows a generic message on a 500 error", async () => {
    mockedRosterService.listUsers.mockResolvedValue([studentA]);
    mockedRosterService.updateUser.mockRejectedValue(new ApiError(500, "Internal error"));

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("alice@school.edu");
    await user.click(screen.getByRole("button", { name: /edit/i }));
    await user.click(screen.getByRole("button", { name: /save/i }));

    expect(await screen.findByText("Something went wrong. Please try again.")).toBeInTheDocument();
  });
});
