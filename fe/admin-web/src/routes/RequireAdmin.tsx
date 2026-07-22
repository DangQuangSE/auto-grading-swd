import { Navigate, Outlet } from "react-router-dom";
import { courseEnrollmentEnabled } from "../lib/features";
import { useAuth } from "../providers/AuthProvider";

export function RequireAdmin() {
  const { session } = useAuth();
  return courseEnrollmentEnabled && session?.user.role === "admin"
    ? <Outlet />
    : <Navigate to="/dashboard" replace />;
}
