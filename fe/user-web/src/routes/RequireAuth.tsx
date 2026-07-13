import { Navigate, Outlet } from "react-router-dom";
import { StateBlock } from "../components/ui/StateBlock";
import { useAuth } from "../providers/AuthProvider";

const ADMIN_WEB_URL = (import.meta.env.VITE_ADMIN_WEB_URL as string | undefined) ?? "http://localhost:5174";

export function RequireAuth() {
  const { session, isLoadingSession } = useAuth();

  if (isLoadingSession) {
    return (
      <main className="auth-shell">
        <StateBlock title="Loading session" />
      </main>
    );
  }

  if (!session) {
    return <Navigate to="/login" replace />;
  }

  // user-web is the student-only portal; lecturer/admin accounts belong in the separate
  // admin-web app (different origin, so this is a full navigation, not an in-app route).
  if (session.user.role === "lecturer" || session.user.role === "admin") {
    window.location.href = ADMIN_WEB_URL;
    return (
      <main className="auth-shell">
        <StateBlock title="Redirecting to the admin workspace..." />
      </main>
    );
  }

  return <Outlet />;
}
