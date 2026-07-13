import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/apiClient";
import * as classService from "../services/classService";
import { ClassManagementPage } from "./ClassManagementPage";

vi.mock("../services/classService");

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

  return render(<ClassManagementPage />, { wrapper: Wrapper });
}

const lecturerA = { id: "lecturer-a", email: "a@school.edu", fullName: "Lecturer A" };
const lecturerB = { id: "lecturer-b", email: "b@school.edu", fullName: "Lecturer B" };

beforeEach(() => {
  vi.resetAllMocks();
  mockedClassService.fetchLecturers.mockResolvedValue([lecturerA, lecturerB]);
});

describe("ClassManagementPage", () => {
  it("renders the class list with resolved lecturer names", async () => {
    mockedClassService.getClasses.mockResolvedValue([
      { id: "class-1", name: "SE1801", lecturerId: lecturerA.id },
    ]);

    renderPage();

    const row = (await screen.findByText("SE1801")).closest("tr")!;
    expect(within(row).getByText(`${lecturerA.fullName} (${lecturerA.email})`)).toBeInTheDocument();
  });

  it("shows an empty state when there are no classes", async () => {
    mockedClassService.getClasses.mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText("No classes yet")).toBeInTheDocument();
  });

  it("creates a new class and clears the form on success", async () => {
    mockedClassService.getClasses.mockResolvedValue([]);
    mockedClassService.createClass.mockResolvedValue({ id: "class-2", name: "SE1802", lecturerId: lecturerB.id });

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("No classes yet");

    await user.type(screen.getByPlaceholderText("SE1801"), "SE1802");
    await user.selectOptions(screen.getByLabelText("Lecturer"), lecturerB.id);
    await user.click(screen.getByRole("button", { name: /create class/i }));

    await waitFor(() => {
      expect(mockedClassService.createClass.mock.calls[0]?.[0]).toEqual({ name: "SE1802", lecturerId: lecturerB.id });
    });
    expect(screen.getByPlaceholderText("SE1801")).toHaveValue("");
  });

  it("shows an error message when class creation fails", async () => {
    mockedClassService.getClasses.mockResolvedValue([]);
    mockedClassService.createClass.mockRejectedValue(new ApiError(400, "Class name already exists"));

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("No classes yet");

    await user.type(screen.getByPlaceholderText("SE1801"), "SE1802");
    await user.selectOptions(screen.getByLabelText("Lecturer"), lecturerB.id);
    await user.click(screen.getByRole("button", { name: /create class/i }));

    expect(await screen.findByText("Class name already exists")).toBeInTheDocument();
  });

  it("reassigns a class's lecturer and updates the table", async () => {
    mockedClassService.getClasses.mockResolvedValue([
      { id: "class-1", name: "SE1801", lecturerId: lecturerA.id },
    ]);
    mockedClassService.updateClassLecturer.mockResolvedValue({
      id: "class-1",
      name: "SE1801",
      lecturerId: lecturerB.id,
    });

    const user = userEvent.setup();
    renderPage();

    const row = (await screen.findByText("SE1801")).closest("tr")!;
    await user.click(within(row).getByRole("button", { name: /reassign/i }));
    await user.selectOptions(within(row).getByRole("combobox"), lecturerB.id);
    await user.click(within(row).getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(mockedClassService.updateClassLecturer).toHaveBeenCalledWith("class-1", lecturerB.id);
    });
  });

  it("shows an error message when reassignment fails", async () => {
    mockedClassService.getClasses.mockResolvedValue([
      { id: "class-1", name: "SE1801", lecturerId: lecturerA.id },
    ]);
    mockedClassService.updateClassLecturer.mockRejectedValue(new ApiError(500, "Server error"));

    const user = userEvent.setup();
    renderPage();

    const row = (await screen.findByText("SE1801")).closest("tr")!;
    await user.click(within(row).getByRole("button", { name: /reassign/i }));
    await user.selectOptions(within(row).getByRole("combobox"), lecturerB.id);
    await user.click(within(row).getByRole("button", { name: /save/i }));

    expect(await screen.findByText("Server error")).toBeInTheDocument();
  });
});
