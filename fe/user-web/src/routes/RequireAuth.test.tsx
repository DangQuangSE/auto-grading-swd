import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as AuthProviderModule from "../providers/AuthProvider";
import { RequireAuth } from "./RequireAuth";

vi.mock("../providers/AuthProvider", async () => {
  const actual = await vi.importActual<typeof import("../providers/AuthProvider")>("../providers/AuthProvider");
  return { ...actual, useAuth: vi.fn() };
});

const mockedUseAuth = vi.mocked(AuthProviderModule.useAuth);

function renderWithGuard() {
  return render(
    <MemoryRouter initialEntries={["/submit"]}>
      <Routes>
        <Route element={<RequireAuth />}>
          <Route path="/submit" element={<div>Student area</div>} />
        </Route>
        <Route path="/login" element={<div>Login page</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("RequireAuth", () => {
  let hrefSetter: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    hrefSetter = vi.fn();
    Object.defineProperty(window, "location", {
      configurable: true,
      value: {
        get href() {
          return "";
        },
        set href(value: string) {
          hrefSetter(value);
        },
      },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("redirects to /login when there is no session", () => {
    mockedUseAuth.mockReturnValue({
      session: null,
      isLoadingSession: false,
      authNotice: null,
      refreshSession: vi.fn(),
      signOutUser: vi.fn(),
    });

    renderWithGuard();

    expect(screen.getByText("Login page")).toBeInTheDocument();
  });

  it("renders the protected route for a student session", () => {
    mockedUseAuth.mockReturnValue({
      session: { token: "t", user: { id: "1", email: "a@b.edu", role: "student" } },
      isLoadingSession: false,
      authNotice: null,
      refreshSession: vi.fn(),
      signOutUser: vi.fn(),
    });

    renderWithGuard();

    expect(screen.getByText("Student area")).toBeInTheDocument();
  });

  it("redirects a lecturer session to the admin-web origin instead of rendering the student route", () => {
    mockedUseAuth.mockReturnValue({
      session: { token: "t", user: { id: "1", email: "a@b.edu", role: "lecturer" } },
      isLoadingSession: false,
      authNotice: null,
      refreshSession: vi.fn(),
      signOutUser: vi.fn(),
    });

    renderWithGuard();

    expect(screen.queryByText("Student area")).not.toBeInTheDocument();
    expect(screen.getByText(/redirecting to the admin workspace/i)).toBeInTheDocument();
    expect(hrefSetter).toHaveBeenCalledWith("http://localhost:5174");
  });

  it("redirects an admin session to the admin-web origin", () => {
    mockedUseAuth.mockReturnValue({
      session: { token: "t", user: { id: "1", email: "a@b.edu", role: "admin" } },
      isLoadingSession: false,
      authNotice: null,
      refreshSession: vi.fn(),
      signOutUser: vi.fn(),
    });

    renderWithGuard();

    expect(hrefSetter).toHaveBeenCalledWith("http://localhost:5174");
  });
});
