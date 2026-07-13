import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, clearStoredSession, setStoredSession } from "../lib/apiClient";
import { AuthProvider } from "../providers/AuthProvider";
import * as authService from "../services/authService";
import * as classService from "../services/classService";
import { LoginPage } from "./LoginPage";

vi.mock("@react-oauth/google", () => ({
  GoogleLogin: () => null,
}));

vi.mock("../services/authService", async () => {
  const actual = await vi.importActual<typeof import("../services/authService")>("../services/authService");
  return {
    ...actual,
    signInWithEmail: vi.fn(actual.signInWithEmail),
    signUpWithEmail: vi.fn(),
  };
});

vi.mock("../services/classService");

const mockedAuthService = vi.mocked(authService);
const mockedClassService = vi.mocked(classService);

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <AuthProvider>{children}</AuthProvider>
        </MemoryRouter>
      </QueryClientProvider>
    );
  }

  return render(<LoginPage />, { wrapper: Wrapper });
}

async function enterSignupMode() {
  renderPage();
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: /create a new account/i }));
  return user;
}

async function fillRequiredFields(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText("Full name"), "Alice Nguyen");
  await user.type(screen.getByLabelText("Email"), "alice@school.edu");
  await user.type(screen.getByLabelText("Password"), "password123");
}

beforeEach(() => {
  vi.resetAllMocks();
  mockedClassService.getClasses.mockResolvedValue([{ id: "class-1", name: "SE1801" }]);
  mockedAuthService.signUpWithEmail.mockResolvedValue({
    token: "token",
    user: { id: "user-1", email: "alice@school.edu", role: "student" },
  });
});

describe("LoginPage signup", () => {
  it("does not show StudentCode/Class fields in login mode", () => {
    renderPage();

    expect(screen.queryByLabelText(/student id/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^class/i)).not.toBeInTheDocument();
  });

  it("shows StudentCode and Class fields in signup mode, loaded from getClasses", async () => {
    await enterSignupMode();

    expect(screen.getByLabelText(/student id/i)).toBeInTheDocument();
    expect(await screen.findByText("SE1801")).toBeInTheDocument();
    expect(mockedClassService.getClasses).toHaveBeenCalled();
  });

  it("submits signup with both StudentCode and ClassId filled", async () => {
    const user = await enterSignupMode();
    await fillRequiredFields(user);
    await screen.findByText("SE1801");

    await user.type(screen.getByLabelText(/student id/i), "SE100001");
    await user.selectOptions(screen.getByLabelText(/^class/i), "class-1");
    await user.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => {
      expect(mockedAuthService.signUpWithEmail).toHaveBeenCalledWith(
        expect.objectContaining({ studentCode: "SE100001", classId: "class-1" }),
      );
    });
  });

  it("submits signup with StudentCode filled but no ClassId", async () => {
    const user = await enterSignupMode();
    await fillRequiredFields(user);
    await screen.findByText("SE1801");

    await user.type(screen.getByLabelText(/student id/i), "SE100001");
    await user.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => {
      expect(mockedAuthService.signUpWithEmail).toHaveBeenCalledWith(
        expect.objectContaining({ studentCode: "SE100001", classId: undefined }),
      );
    });
  });

  it("submits signup with ClassId filled but no StudentCode", async () => {
    const user = await enterSignupMode();
    await fillRequiredFields(user);
    await screen.findByText("SE1801");

    await user.selectOptions(screen.getByLabelText(/^class/i), "class-1");
    await user.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => {
      expect(mockedAuthService.signUpWithEmail).toHaveBeenCalledWith(
        expect.objectContaining({ studentCode: undefined, classId: "class-1" }),
      );
    });
  });

  it("submits signup with neither StudentCode nor ClassId (both optional)", async () => {
    const user = await enterSignupMode();
    await fillRequiredFields(user);
    await screen.findByText("SE1801");

    await user.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => {
      expect(mockedAuthService.signUpWithEmail).toHaveBeenCalledWith(
        expect.objectContaining({ studentCode: undefined, classId: undefined }),
      );
    });
  });

  it("shows the server's error message on a 400 'class not found' response", async () => {
    mockedAuthService.signUpWithEmail.mockRejectedValue(
      new ApiError(400, "Class not found or not yet synchronized; please try again or contact your administrator."),
    );

    const user = await enterSignupMode();
    await fillRequiredFields(user);
    await screen.findByText("SE1801");
    await user.selectOptions(screen.getByLabelText(/^class/i), "class-1");
    await user.click(screen.getByRole("button", { name: /create account/i }));

    expect(
      await screen.findByText("Class not found or not yet synchronized; please try again or contact your administrator."),
    ).toBeInTheDocument();
  });

  it("shows the assigned class name in the success message", async () => {
    const user = await enterSignupMode();
    await fillRequiredFields(user);
    await screen.findByText("SE1801");
    await user.selectOptions(screen.getByLabelText(/^class/i), "class-1");
    await user.click(screen.getByRole("button", { name: /create account/i }));

    expect(await screen.findByText("Account created. You're in class SE1801.")).toBeInTheDocument();
  });

  it("rejects a spaces-only StudentCode", async () => {
    const user = await enterSignupMode();
    await fillRequiredFields(user);
    await screen.findByText("SE1801");

    await user.type(screen.getByLabelText(/student id/i), "   ");
    await user.click(screen.getByRole("button", { name: /create account/i }));

    expect(await screen.findByText(/spaces only/i)).toBeInTheDocument();
    expect(mockedAuthService.signUpWithEmail).not.toHaveBeenCalled();
  });

  it("clears StudentCode and Class when toggling back to login and then back to signup", async () => {
    const user = await enterSignupMode();
    await screen.findByText("SE1801");

    await user.type(screen.getByLabelText(/student id/i), "SE100001");
    await user.selectOptions(screen.getByLabelText(/^class/i), "class-1");

    await user.click(screen.getByRole("button", { name: /use an existing account/i }));
    await user.click(screen.getByRole("button", { name: /create a new account/i }));
    await screen.findByText("SE1801");

    expect(screen.getByLabelText(/student id/i)).toHaveValue("");
    expect(screen.getByLabelText(/^class/i)).toHaveValue("");
  });

  it("stays on the login form when a stale lecturer/admin session is already stored", () => {
    setStoredSession({ token: "stale-token", user: { id: "u1", email: "lecturer@school.edu", role: "lecturer" } });

    try {
      renderPage();

      expect(screen.getByRole("heading", { name: /sign in/i })).toBeInTheDocument();
    } finally {
      clearStoredSession();
    }
  });
});
