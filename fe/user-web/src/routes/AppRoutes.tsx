import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { StateBlock } from "../components/ui/StateBlock";
import { RequireAuth } from "./RequireAuth";

const LecturerDashboard = lazy(() =>
  import("../pages/LecturerDashboard").then((module) => ({ default: module.LecturerDashboard })),
);
const LoginPage = lazy(() => import("../pages/LoginPage").then((module) => ({ default: module.LoginPage })));
const RubricUploadPage = lazy(() =>
  import("../pages/RubricUploadPage").then((module) => ({ default: module.RubricUploadPage })),
);
const StudentResultPage = lazy(() =>
  import("../pages/StudentResultPage").then((module) => ({ default: module.StudentResultPage })),
);
const StudentSubmissionPage = lazy(() =>
  import("../pages/StudentSubmissionPage").then((module) => ({ default: module.StudentSubmissionPage })),
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
            <Route path="/dashboard" element={<LecturerDashboard />} />
            <Route path="/rubrics" element={<RubricUploadPage />} />
            <Route path="/submit" element={<StudentSubmissionPage />} />
            <Route path="/review" element={<SubmissionReviewPage />} />
            <Route path="/review/:submissionId" element={<SubmissionReviewPage />} />
            <Route path="/result" element={<StudentResultPage />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </Suspense>
  );
}
