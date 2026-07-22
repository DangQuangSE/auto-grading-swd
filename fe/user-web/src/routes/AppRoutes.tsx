import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { StateBlock } from "../components/ui/StateBlock";
import { RequireAuth } from "./RequireAuth";
import { courseEnrollmentEnabled } from "../lib/features";

const LoginPage = lazy(() => import("../pages/LoginPage").then((module) => ({ default: module.LoginPage })));
const StudentResultPage = lazy(() =>
  import("../pages/StudentResultPage").then((module) => ({ default: module.StudentResultPage })),
);
const StudentSubmissionPage = lazy(() =>
  import("../pages/StudentSubmissionPage").then((module) => ({ default: module.StudentSubmissionPage })),
);
const StudentProfilePage = lazy(() =>
  import("../pages/StudentProfilePage").then((module) => ({ default: module.StudentProfilePage })),
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
            <Route index element={<Navigate to="/submit" replace />} />
            <Route path="/submit" element={<StudentSubmissionPage />} />
            <Route path="/result/:submissionId" element={<StudentResultPage />} />
            <Route path="/result" element={<StudentResultPage />} />
            {courseEnrollmentEnabled ? <Route path="/profile" element={<StudentProfilePage />} /> : null}
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/submit" replace />} />
      </Routes>
    </Suspense>
  );
}
