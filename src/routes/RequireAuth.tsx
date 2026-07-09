import { Navigate, Outlet } from "react-router-dom";
import { StateBlock } from "../components/ui/StateBlock";
import { useAuth } from "../providers/AuthProvider";

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

  return <Outlet />;
}
