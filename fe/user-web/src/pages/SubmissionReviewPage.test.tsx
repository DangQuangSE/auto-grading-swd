import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { SubmissionReviewPage } from "./SubmissionReviewPage";

vi.mock("../hooks/useSubmissions", () => ({
  useRecentSubmissions: () => ({
    data: [],
    isLoading: false,
  }),
  useSubmissionReview: () => ({
    data: undefined,
    isLoading: false,
  }),
  useSaveFinalScore: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
  usePublishGrade: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

vi.mock("../providers/AuthProvider", () => ({
  useAuth: () => ({
    session: { user: { id: "lecturer-id" } },
  }),
}));

describe("SubmissionReviewPage", () => {
  it("renders the review selection empty state", () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <SubmissionReviewPage />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(screen.getByText(/select a submission/i)).toBeInTheDocument();
  });
});
