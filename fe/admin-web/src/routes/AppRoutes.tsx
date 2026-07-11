import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { StateBlock } from "../components/ui/StateBlock";
import { RequireAuth } from "./RequireAuth";

const DashboardPage = lazy(() => import("../pages/DashboardPage").then((module) => ({ default: module.DashboardPage })));
const LoginPage = lazy(() => import("../pages/LoginPage").then((module) => ({ default: module.LoginPage })));
const SubjectsPage = lazy(() =>
  import("../pages/SubjectsPage").then((module) => ({ default: module.SubjectsPage })),
);
const AssignmentsPage = lazy(() =>
  import("../pages/AssignmentsPage").then((module) => ({ default: module.AssignmentsPage })),
);
const RubricUploadPage = lazy(() =>
  import("../pages/RubricUploadPage").then((module) => ({ default: module.RubricUploadPage })),
);
const SubmissionReviewPage = lazy(() =>
  import("../pages/SubmissionReviewPage").then((module) => ({ default: module.SubmissionReviewPage })),
);

export function AppRoutes() {
  return (
    <Suspense
      fallback={
        <main className="auth-shell">
          <StateBlock title="Loading page" />
        </main>
      }
    >
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireAuth />}>
          <Route element={<AppShell />}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/subjects" element={<SubjectsPage />} />
            <Route path="/assignments" element={<AssignmentsPage />} />
            <Route path="/rubrics" element={<RubricUploadPage />} />
            <Route path="/review" element={<SubmissionReviewPage />} />
            <Route path="/review/:submissionId" element={<SubmissionReviewPage />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </Suspense>
  );
}
