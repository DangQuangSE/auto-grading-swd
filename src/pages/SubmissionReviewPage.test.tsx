import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SubmissionReviewPage } from "./SubmissionReviewPage";

describe("SubmissionReviewPage", () => {
  it("renders extracted evidence and editable final score inputs", () => {
    render(<SubmissionReviewPage />);

    expect(screen.getByText(/extracted evidence/i)).toBeInTheDocument();
    expect(screen.getByText(/criterion scores/i)).toBeInTheDocument();
    expect(screen.getByText(/architecture consistency/i)).toBeInTheDocument();
    expect(screen.getAllByLabelText(/final/i)).toHaveLength(2);
  });
});
